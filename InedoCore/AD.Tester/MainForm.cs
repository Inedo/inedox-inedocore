using System;
using System.Windows.Forms;
using Inedo.Diagnostics;
using Inedo.Extensibility.UserDirectories;
using Inedo.Extensions.UserDirectories;

namespace AD.Tester
{
    public partial class MainForm : Form
    {
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
    }
}
