using System.Threading.Tasks;
using NUnit.Framework;

namespace UITS.Box.Collaborations.Tests
{
    [TestFixture]
    public class IntegrationTests
    {
        private const string Username = "jhoerr";
        private const string AccessToken = "ACCESS_TOKEN";
        private const string OutFilePath = @"c:\temp\update.csv";

        // Specify a filter for the domains you'd like to include in your report. An empty collection allows collaborations from *any* domain.
        // private static readonly string[] CollaborationDomainWhitelist = {"myschool1.edu", "myschool2.edu"};
        private static readonly string[] CollaborationDomainWhitelist = new string[0];

        [TestCase("owner", Description = "Record info about collaborations that I own")]
        [TestCase("member", Description = "Record info about collaborations of which I'm a member")]
        public async Task RecordMemberCollaborations(string role)
        {
            await Program.Do(AccessToken, Username, role, null, OutFilePath, CollaborationDomainWhitelist);
        }
    }
}