using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Inedo;
using Inedo.Configuration;
using Inedo.Diagnostics;
using Inedo.Extensions.Credentials;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensibility.UserDirectories;
using Inedo.Extensions.UserDirectories;
using Inedo.Security;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;
using Inedo.Serialization;
using System.Net;

namespace AD.Tester
{
    public partial class MainForm : Form
    {
        private static bool initialized;
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            cbSearchMode.SelectedIndex = 0;
        }

        private void Exec(Action a, KeyEventArgs e = null)
        {
            if (e != null && e.KeyCode != Keys.Return && e.KeyCode != Keys.Enter)
                return;

            txtLogs.Clear();
            Log(MessageLevel.Information, "Operation executing...");

            try
            {
                a();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log(MessageLevel.Error, ex.ToString());
            }

            Log(MessageLevel.Information, "Operation completed.");
        }

        private void btnSearch_Click(object sender, EventArgs e) => Exec(Search);
        private void txtSearch_KeyUp(object sender, KeyEventArgs e) => Exec(Search, e);
        private void btnGetUser_Click(object sender, EventArgs e) => Exec(GetUser);
        private void txtUserName_KeyUp(object sender, KeyEventArgs e) => Exec(GetUser, e);
        private void txtGroup_KeyUp(object sender, KeyEventArgs e) => Exec(GetUser, e);

        private void GetUser()
        {
            var dir = this.CreateDirectory();

            var user = dir.TryGetUser(txtUserName.Text);

            gvResults.Rows.Clear();
            if (user != null)
            {
                AddToGrid(user);

                if (!string.IsNullOrEmpty(txtGroup.Text))
                {
                    bool isMember = user.IsMemberOfGroup(txtGroup.Text);
                    Log(MessageLevel.Information, "Member of group result: " + isMember);
                }
            }
        }

        private void Search()
        {
            var dir = this.CreateDirectory();

            var principals = dir.FindPrincipals(txtSearch.Text);

            gvResults.Rows.Clear();
            foreach (var p in principals)
                AddToGrid(p);
        }

        private void AddToGrid(IUserDirectoryPrincipal p)
        {
            gvResults.Rows.Add(
                    p is IUserDirectoryGroup ? "Group" : "User",
                    p.Name,
                    p.DisplayName,
                    (p as IUserDirectoryUser)?.EmailAddress ?? "-"
                );
        }

        private ADUserDirectory CreateDirectory()
        {
            if (!initialized)
            {
                InedoSdkConfig.Initialize(new Config(this.GetCredentialSettings));
                initialized = true;
            }

            var dir = new ADUserDirectory
            {
                SearchMode = ToSearchMode(cbSearchMode.SelectedIndex),
                NetBiosNameMaps = ToArray(txtNetbiosNames.Text),
                DomainControllerAddress = NullIfEmpty(txtDomainControllerHost.Text),
                DomainsToSearch = ToArray(txtDomains.Text),
                SearchGroupsRecursively = cblOptions.SelectedIndices.Contains(0),
                IncludeGroupManagedServiceAccounts = cblOptions.SelectedIndices.Contains(1),
                UseLdaps = cblOptions.SelectedIndices.Contains(2),
            };

            dir.MessageLogged += this.MessageLogged;

            return dir;
        }

        private void MessageLogged(object sender, LogMessageEventArgs e) => Log(e.Level, e.Message);

        private void Log(MessageLevel level, string m)
        {
            txtLogs.AppendText(LevelText(level));
            txtLogs.AppendText(m);
            txtLogs.AppendText(Environment.NewLine);
        }

        private string LevelText(MessageLevel level)
        {
            switch (level)
            {
                case MessageLevel.Debug: return "DEBUG: ";
                case MessageLevel.Information: return "INFO : ";
                case MessageLevel.Warning: return "WARN : ";
                case MessageLevel.Error: return "ERROR: ";
                default: return "UNKWN:";
            }
        }

        private ADSearchMode ToSearchMode(int i)
        {
            switch (i)
            {
                case 1: return ADSearchMode.TrustedDomains;
                case 2: return ADSearchMode.SpecificDomains;
                default: return ADSearchMode.CurrentDomain;
            }
        }

        private static string[] ToArray(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            else
                return s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string NullIfEmpty(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            else
                return s;
        }

        private static string[] NullIfEmpty(string[] s)
        {
            if (s == null || s.Length == 0)
                return null;
            else
                return s;
        }

        private void parseLogin_Click(object sender, EventArgs e)
        {
            var dir = this.CreateDirectory();

            var principal = dir.TryParseLogonUser(txtParseLogin.Text);

            gvResults.Rows.Clear();
            if(principal != null)
                AddToGrid(principal);
        }


        private void btnGetUsersForGrou_OnClick(object sender, EventArgs e)
        {
            var dir = this.CreateDirectory();

            var principals = dir.GetGroupMembers(tbGroupUsersSearch.Text);

            gvResults.Rows.Clear();
            if (principals != null)
            {
                foreach(var principal in principals)
                {
                    AddToGrid(principal);
                }
            }
        }

        private UsernamePasswordCredentials GetCredentialSettings()
        {
            if (string.IsNullOrWhiteSpace(txtDomainUserName.Text))
                return null;
            var adCreds = new UsernamePasswordCredentials();
            adCreds.UserName = txtDomainUserName.Text;
            adCreds.Password = new NetworkCredential("", txtDomainPassword.Text).SecurePassword;

            return adCreds;
        }

        private sealed class Config : InedoSdkConfig
        {
            private Func<UsernamePasswordCredentials> getCreds;

            public Config(Func<UsernamePasswordCredentials> getCreds)
            {
                this.getCreds = getCreds;
            }

            public override IEnumerable<SDK.CredentialsInfo> GetCredentials()
            {
                var credsList = new List<SDK.CredentialsInfo>();

                var creds = this.getCreds();
                if(creds != null)
                {
                    credsList.Add(new SDK.CredentialsInfo(1, "UsernamePassword", "AdCreds", Persistence.SerializeToPersistedObjectXml(creds), null, null));
                }

                return credsList;
            }

            public override string BaseUrl => throw new NotImplementedException();
            public override string ProductName => throw new NotImplementedException();
            public override Version ProductVersion => throw new NotImplementedException();
            public override Type SecuredTaskType => throw new NotImplementedException();
            public override string DefaultRaftName => throw new NotImplementedException();
            public override UserDirectory CreateUserDirectory(int userDirectoryId) => throw new NotImplementedException();
            public override string GetConfigValue(string configKey) => throw new NotImplementedException();
            public override SDK.CredentialsInfo GetCredentialById(int id) => throw new NotImplementedException();
            public override ITaskChecker GetCurrentTaskChecker() => throw new NotImplementedException();
            public override IUserDirectoryUser GetCurrentUser() => throw new NotImplementedException();
            public override UserDirectory GetCurrentUserDirectory() => throw new NotImplementedException();
            public override IEnumerable<SDK.EnvironmentInfo> GetEnvironments() => throw new NotImplementedException();
            public override IEnumerable<SDK.ProjectInfo> GetProjects() => throw new NotImplementedException();
            public override SDK.RaftItemInfo GetRaftItem(RaftItemType type, string itemId, object context) => throw new NotImplementedException();
            public override IEnumerable<SDK.RaftItemInfo> GetRaftItems(RaftItemType type, object context) => throw new NotImplementedException();
            public override SDK.SecureResourceInfo GetSecureResourceById(int id) => throw new NotImplementedException();
            public override IEnumerable<SDK.SecureResourceInfo> GetSecureResources() => throw new NotImplementedException();
            public override IEnumerable<SDK.ServerRoleInfo> GetServerRoles() => throw new NotImplementedException();
            public override IEnumerable<SDK.ServerInfo> GetServers(bool includeInactive) => throw new NotImplementedException();
            public override IEnumerable<SDK.ServerInfo> GetServersInEnvironment(int environmentId) => throw new NotImplementedException();
            public override IEnumerable<SDK.ServerInfo> GetServersInRole(int roleId) => throw new NotImplementedException();
            public override IEnumerable<SDK.UserDirectoryInfo> GetUserDirectories() => throw new NotImplementedException();
        }

        private void label11_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            var dir = this.CreateDirectory();

            var user = dir.TryGetAndValidateUser(txtLoginUsername.Text, txtLoginPassword.Text);

            gvResults.Rows.Clear();
            if (user != null)
            {
                AddToGrid(user);
            }
        }
    }
}
