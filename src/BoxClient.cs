using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Box.V2.Auth;
using Box.V2.Config;
using Box.V2.Converter;
using Box.V2.Exceptions;
using Box.V2.Managers;
using Box.V2.Models;
using Box.V2.Request;
using Box.V2.Services;
using NLog;
using UITS.Box.Collaborations.BoxExtensions;
using UITS.Box.Collaborations.IO;
using UITS.Box.Collaborations.Model;

namespace UITS.Box.Collaborations
{
    public class BoxClient
    {
        private static Logger Log = LogManager.GetCurrentClassLogger();

        private readonly string _outPath;
        private static BoxConfig _boxConfig;
        private static AuthRepository _authRepository;

        private static readonly List<string> CollabFields = new List<string>(){"item", "accessible_by", "created_by", "role"};

        public BoxClient(string accessToken, string outPath)
        {
            _outPath = outPath;
            _boxConfig = new BoxConfig("key", "secret", new Uri("https://baz"));
            _authRepository = new AuthRepository(_boxConfig, new BoxService(new HttpRequestHandler()), new BoxJsonConverter(), new OAuthSession(accessToken, "refresh", 10, "bearer"));
        }

        public async Task RecordCollaborationsAsync(string outPath, List<string> logins, string[] domainWhitelist, string role)
        {
            // Record collaborations for up to 4 users in parallel. A few users will have absurdly deep Box accounts, so this prevents them from gumming up the works.
            // This value can be safely increased, but at some point Box will start throttling requests.
            const int maxDegreeOfParallelism = 4;

            var tasks = new List<Task>();
            var throttler = new SemaphoreSlim(maxDegreeOfParallelism);
            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            foreach (var login in logins)
            {
                await throttler.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await RecordCollaborationsForUserAsync(outPath, login, domainWhitelist, role, ct);
                    }
                    catch
                    {
                        cts.Cancel();
                    }
                    finally
                    {
                        throttler.Release();
                    }
                }, ct));
            }
            await Task.WhenAll(tasks);
        }

        private static async Task RecordCollaborationsForUserAsync(string outPath, string login, string[] domainWhitelist, string role, CancellationToken ct)
        {
            try
            {
                // Fetch all the Box users associated with this username. This allows for the existence of duplicate accounts in your domain.
                List<BoxUser> users = await GetBoxUsersAsync(login);
                if (UserFoundForLogin(users, login))
                {
                    LogMultipleUsersForSingleLogin(users, login);
                    foreach (BoxUser user in users)
                    {
                        // Record collaboration info for each Box user instance. 
                        Log.Info("Started reviewing {0} ({1})", user.Login, login);
                        // Get the groups that this user belongs to. If a collaboration is with a group, and they're a member, we'll want to record that.
                        var groupIds = await GroupIdsForUser(user);
                        var collaborations = new List<Collaboration>();
                        await GatcherCollaborationsForBoxUserAsync(NewBoxFoldersManager(user), user, role, groupIds, collaborations);
                        await ResultsWriter.WriteResultsAsync(outPath, collaborations, domainWhitelist);
                        Log.Info("Finished reviewing {0} ({1})", user.Login, login);
                    }
                }
            }
            catch (BoxException e)
            {
                if (e.StatusCode.Equals(HttpStatusCode.Unauthorized))
                {
                    Log.Error("Authorization has expired");
                    throw new AuthorizationExpiredException();                    
                }
                Log.ErrorException(String.Format("Failed to find collaborations for '{0}' due to Box exception: {1}", login, e.Message), e);
            }
            catch (Exception e)
            {
                Log.ErrorException(String.Format("Failed to find collaborations for '{0}' due to generic exception: {1}", login, e.Message), e);
            }
        }

        private static async Task<List<string>> GroupIdsForUser(BoxUser user)
        {
            var boxGroupsManager = new BoxGroupsManager(_boxConfig, new OnBehalfOfUserService(new HttpRequestHandler(), user.Id), new BoxJsonConverter(), _authRepository);
            var groupMembershipsForUser = await boxGroupsManager.GetAllGroupMembershipsForUserAsync(user.Id);
            var groupIds = groupMembershipsForUser.Entries.Select(e => e.Id).ToList();
            return groupIds;
        }

        private static BoxFoldersManager NewBoxFoldersManager(BoxUser user)
        {
            return new BoxFoldersManager(_boxConfig, new OnBehalfOfUserService(new HttpRequestHandler(), user.Id), new BoxJsonConverter(), _authRepository);
        }

        private static void LogMultipleUsersForSingleLogin(List<BoxUser> users, string login)
        {
            if (users.Count() > 1)
            {
                Log.Warn("Found {0} Box users for login {1}: {2}", users.Count(), login, String.Join(", ", users.Select(u => u.Login)));
            }
        }

        private static bool UserFoundForLogin(List<BoxUser> users, string login)
        {
            bool any = users.Any();
            if (!any)
            {
                Log.Warn("No Box user found for login {0}", login);
            }
            return any;
        }

        private static async Task GatcherCollaborationsForBoxUserAsync(BoxFoldersManager folderManager, BoxUser user, string role, List<string> groupIds, List<Collaboration> result, string folderId = "0")
        {
            // Get all the subfolders of this current folder
            var subfolders = await GetAllSubfoldersAsync(folderManager, folderId);
            
            // Find all collaborations within this folder that meet our 'role' criteria
            var foldersWithCollaborations = subfolders.Where(f => f.HasCollaborations.GetValueOrDefault(false)).ToList();
            await ReviewCollaborationsAsync(folderManager, foldersWithCollaborations, user, role, groupIds, result);

            // Recursively scan for collaborations in folders that *don't* already have collaborations.
            // Once we've determined that a folder has collaborations, we don't need to dive any deeper into it.
            foreach (var folder in subfolders.Except(foldersWithCollaborations, BoxFolderEqualityComparer.ById))
            {
                await GatcherCollaborationsForBoxUserAsync(folderManager, user, role, groupIds, result, folder.Id);
            }
        }

        private async static Task ReviewCollaborationsAsync(BoxFoldersManager folderManager, IEnumerable<BoxFolder> collaborationFolders, BoxUser user, string role, List<string> groupIds, List<Collaboration> result)
        {
            switch (role)
            {
                case "owner":
                    await ReviewOwnedCollaborationsAsync(folderManager, collaborationFolders, user, result);
                    break;
                case "member":
                    await ReviewMemberCollaborationsAsync(folderManager, collaborationFolders, user, groupIds, result);
                    break;
                default:
                    throw new ArgumentException("Must be 'owner' or 'member'", "role");
            }
        }

        /// <summary>
        /// Record the folders, members, and roles of any collaborations owned by this user.
        /// </summary>
        private static async Task ReviewOwnedCollaborationsAsync(BoxFoldersManager folderManager, IEnumerable<BoxFolder> collaborationFolders, BoxUser user, List<Collaboration> result)
        {
            var ownedByThisUser = collaborationFolders.Where(c => c.OwnedBy.Id.Equals(user.Id)).ToList();
            foreach (var folder in ownedByThisUser)
            {
                var collaborations = await folderManager.GetCollaborationsAsync(folder.Id, CollabFields);
                result.AddRange(collaborations.Entries.Select(c => new Collaboration(c, user)));
            }
        }

        /// <summary>
        /// Record folders, owners, and roles of any collaborations of which this user is a member.
        /// </summary>
        private static async Task ReviewMemberCollaborationsAsync(BoxFoldersManager folderManager, IEnumerable<BoxFolder> collaborationFolders, BoxUser user, List<string> groupIds, List<Collaboration> result)
        {
            foreach (var folder in collaborationFolders)
            {
                var collaborations = await folderManager.GetCollaborationsAsync(folder.Id, CollabFields);
                AddPersonalCollaborations(user, result, collaborations);
                AddGroupCollaborations(groupIds, result, collaborations);
            }
        }

        /// <summary>
        /// Determine collaborations where the member is this user specifically.
        /// </summary>
        private static void AddPersonalCollaborations(BoxUser user, List<Collaboration> result, BoxCollection<BoxCollaboration> collaborations)
        {
            var memberCollab = collaborations.Entries.SingleOrDefault(c => c.AccessibleBy.Id.Equals(user.Id));
            if (memberCollab == null) return;
            result.Add(new Collaboration(memberCollab, memberCollab.CreatedBy));
        }

        /// <summary>
        /// Determine collaborations where the member is a group that this user is in.
        /// </summary>
        private static void AddGroupCollaborations(List<string> groupIds, List<Collaboration> result, BoxCollection<BoxCollaboration> collaborations)
        {
            var groupCollabs = collaborations.Entries.Where(c => groupIds.Any(gid => c.AccessibleBy.Id.Equals(gid))).ToList();
            result.AddRange(groupCollabs.Select(c => new Collaboration(c, c.CreatedBy)));
        }

        private static async Task<IList<BoxFolder>> GetAllSubfoldersAsync(BoxFoldersManager foldersManager, string folderId)
        {
            try
            {
                var items = new List<BoxItem>();
                var itemsReceivedOnThisCall = 0;
                var itemsReceivedTotal = 0;
                var totalItemsCount = 0;
                var retry = true;
                do
                {
                    var fields = new List<string> {BoxFolder.FieldName, BoxFolder.FieldHasCollaborations, BoxFolder.FieldOwnedBy};
                    BoxCollection<BoxItem> result = await foldersManager.GetFolderItemsAsync(folderId, 1000, itemsReceivedTotal, fields);
                    items.AddRange(result.Entries);
                    totalItemsCount = result.TotalCount;
                    itemsReceivedOnThisCall = result.Entries.Count();
                    itemsReceivedTotal += itemsReceivedOnThisCall;

                    if (itemsReceivedOnThisCall == 0 && totalItemsCount != 0)
                    {
                        if (retry)
                        {
                            retry = false;
                        }
                        else
                        {
                            Log.Warn("Received no folder items after retrying once. Bailing.");
                            break;
                        }
                    }

                } while (itemsReceivedTotal < totalItemsCount);

                return items.Where(e => e.Type.Equals("folder")).Cast<BoxFolder>().ToList();
            }
            catch (Exception e)
            {
                Log.Error("Failed to fetch folder with id '{0}': {1}", folderId, e.Message);
            }
            return new BoxFolder[0];
        }

        // Get all Box users in this enterprise for which the username of the provided 'login' matches the left side of the '@' in the Box login.
        private static async Task<List<BoxUser>> GetBoxUsersAsync(string login)
        {
            var userManager = new BoxEnterpriseUsersManager(_boxConfig, new BoxService(new HttpRequestHandler()), new BoxJsonConverter(), _authRepository);
            var searchableLogin = login.Split(new[] { '@' }, StringSplitOptions.RemoveEmptyEntries).First().Trim(new[] { '@' }) + '@';
            var boxUsers = await userManager.GetEnterpriseUsersAsync(searchableLogin);
            return boxUsers.Entries;
        }
    }

    internal class BoxFolderEqualityComparer : IEqualityComparer<BoxFolder>
    {
        public bool Equals(BoxFolder x, BoxFolder y)
        {

            return x != null 
                   && y!= null
                   && !String.IsNullOrWhiteSpace(x.Id)
                   && !String.IsNullOrWhiteSpace(y.Id)
                   && x.Id.Equals(y.Id,StringComparison.InvariantCultureIgnoreCase);
        }

        public int GetHashCode(BoxFolder obj)
        {
            return obj == null ? 0 : String.IsNullOrWhiteSpace(obj.Id) ? 0 : obj.Id.GetHashCode();
        }

        public static BoxFolderEqualityComparer ById {
            get { return new BoxFolderEqualityComparer(); }
        }
    }
}