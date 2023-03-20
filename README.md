<img src="images/box-dev-logo-clip.png" 
alt= “box-dev-logo” 
style="margin-left:-10px;"
width=40%;>
# Collaborations

A Windows-based utility that can answer:

* what folders is a given user collaborating on? 
* who is collaborating on folders owned by a given user?

This utility requires a valid Box access token that has enterprise management privileges in order to determine folder,  collaboration, and group metadata for arbitrary users.

# Usage

Download the `/artifacts` folder on a Windows computer. From this folder you can run `collaborations.exe`.

To record the folders a user is collaborating on:
    
    collaborations.exe -r=member -u=<username> -t=<box access token> -o=<path to result.csv>

To record the collaborators on folders owned by a user:
    
    collaborations.exe -r=owner -u=<username> -t=<box access token> -o=<path to result.csv>

To record collaboration information for a comma-separated file of users, use the *-i* option instead of *-u*

    collaborations.exe -r=member -i=<path to usernames file> -t=<box access token> -o=<path to results file>

# Build Instructions

This solution requires the .Net 4.5 framework and can be built in Visual Studio 2013.
