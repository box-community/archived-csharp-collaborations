using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NDesk.Options;
using Nito.AsyncEx;
using NLog;
using UITS.Box.Collaborations.IO;

namespace UITS.Box.Collaborations
{
    public class Program
    {
        private static Logger Log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            string[] roles = {"owner", "member"};

            string inPath = null;
            string role = null;
            string outPath = null;
            string accesstoken = null;
            string username = null;
            var domains = new string[0];
            bool showHelp = false;

            var p = new OptionSet
            {
                {"t|token=", "Required. A Box Access {TOKEN} with enterprise management capability", t => accesstoken = t},
                {"u|username:", "Required if 'userfile' not set. A single {USERNAME} for which to show Box collaboration information", u => username = u },
                {"i|userfile:", "Required if 'username' not set. A file {PATH} containing a comma- or newline-separated list of usernames for which to show Box collaboration information", i => inPath = i},
                {"o|outpath=", "Required. A destination {PATH} for the collaboration results. If none provided, results will be written to the console.", o => outPath = o},
                {"r|role:", "Optional. Accepted values: owner, member. Default: member. Choosing 'owner' {ROLE} will record the members and folder names of all collaborations for which this user is in the 'owner' role. Choosing 'member' will record the owners and folder names of all collaborations for which this user is in any role other than 'owner'.", r => role = r ?? "member"},
                {"d|domains:", "Optional. Default: all domains. A comma-separated white list of {DOMAINS} for which collaboration members should be recorded.", d => domains = d.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToArray()},
                {"h|help", "show this message and exit", h => showHelp = (h != null)},
            };

            try
            {
                p.Parse(args);

                if (showHelp
                    || string.IsNullOrWhiteSpace(accesstoken)
                    || (string.IsNullOrWhiteSpace(inPath) && string.IsNullOrWhiteSpace(username))
                    || !roles.Any(r => r.Equals(role, StringComparison.InvariantCultureIgnoreCase)))
                {
                    Console.Out.WriteLine("showhelp: " + showHelp.ToString());
                    Console.Out.WriteLine("inPath: " + (inPath??"none"));
                    Console.Out.WriteLine("username: " + (username??"none"));
                    Console.Out.WriteLine("role: " + (role??"none"));
                    ShowHelp(p);
                    return;
                }
            }
            catch (OptionException e)
            {
                ShowHelp(p);
                return;
            }

            AsyncContext.Run(() => Do(accesstoken, username, role, inPath, outPath, domains));
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: collaborations -t <token> [-u <username> -i <infile> -o <outfile> -h]");
            Console.WriteLine("Gather ");
            Console.WriteLine("If no message is specified, a generic greeting is used.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        public static async Task Do(string accessToken, string username, string role, string inPath, string outPath, string[] domainWhitelist)
        {
            try
            {
                // Get a list of logins from the 'inPath' if a 'username' hasn't been specified.
                var allLogins = username == null
                    ? await Parser.GetLoginsFromFile(inPath)
                    : new List<string>() { username };

                var logins = allLogins.Distinct().ToList();

                Log.Info("Gathering collaboration info for {0} logins in the '{1}' role.", logins.Count, role);
                Log.Info("Results will be written to {0}.", outPath);

                await ResultsWriter.WriteHeaderAsync(outPath);
                var boxClient = new BoxClient(accessToken, outPath);
                await boxClient.RecordCollaborationsAsync(outPath, logins, domainWhitelist, role);
            }
            catch (Exception e)
            {
                Log.Fatal("Failed to record collaborations", e);
            }
        }
    }

    internal class AuthorizationExpiredException : Exception
    {
    }
}
