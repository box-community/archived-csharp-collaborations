using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UITS.Box.Collaborations.Model;

namespace UITS.Box.Collaborations.IO
{
    static internal class ResultsWriter
    {
        public static async Task WriteHeaderAsync(string outPath)
        {
            if (File.Exists(outPath)) return;

            using (var stream = new StreamWriter(outPath, true))
            {
                await stream.WriteLineAsync("CollaboratorName,CollaboratorLogin,CollaboratorRole,FolderName,FolderId,FolderOwnerName,FolderOwnerLogin");
            }
        }

        public static async Task WriteResultsAsync(string outPath, IEnumerable<Collaboration> collaborations, string[] domainWhitelist)
        {
            using (var stream = new StreamWriter(outPath, true))
            {
                foreach (var collaboration in collaborations.Where(c => Whitelisted(c, domainWhitelist)))
                {
                    await stream.WriteLineAsync(collaboration.ToString());
                }
            }
        }

        private static bool Whitelisted(Collaboration collaboration, string[] whiteList)
        {
            if (!whiteList.Any()) return true;

            var collabDomain = collaboration.CollaboratorLogin.Split('@').Last();
            var ownerDomain = collaboration.FolderOwnerLogin.Split('@').Last();
            return (collabDomain.Equals(Collaboration.Unknown) || whiteList.Any(d => collabDomain.Equals(d, StringComparison.InvariantCultureIgnoreCase)))
                   || (ownerDomain.Equals(Collaboration.Unknown) || whiteList.Any(d => ownerDomain.Equals(d, StringComparison.InvariantCultureIgnoreCase)));
        }
    }
}