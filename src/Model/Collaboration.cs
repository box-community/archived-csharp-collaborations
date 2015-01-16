using Box.V2.Models;
using UITS.Box.Collaborations.BoxExtensions;

namespace UITS.Box.Collaborations.Model
{
    public class Collaboration : CollaborationSlim
    {
        public const string Unknown = "(unknown)";

        public Collaboration(BoxCollaboration collaboration, BoxUser folderOwner)
        {
            FolderName = collaboration.Item.Name;
            FolderId = collaboration.Item.Id;
            FolderOwnerName = folderOwner == null ? (Unknown) : folderOwner.Name;
            FolderOwnerLogin = folderOwner == null ? (Unknown) : folderOwner.Login;
            CollaboratorName = collaboration.AccessibleBy == null ? (Unknown) : collaboration.AccessibleBy.Name;
            CollaboratorLogin = collaboration.AccessibleBy == null ? (Unknown) : collaboration.AccessibleBy.Login;
            CollaboratorRole = collaboration.Role;
        }

        public string FolderId { get; set; }
        public string FolderOwnerName { get; set; }
        public string FolderOwnerLogin { get; set; }
        public string CollaboratorRole { get; set; }

        public override string ToString()
        {
            return string.Join(",", CollaboratorName, CollaboratorLogin, CollaboratorRole, FolderName, FolderId, FolderOwnerName, FolderOwnerLogin);
        }
    }
}