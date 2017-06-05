using System;
using Newtonsoft.Json;

#if Otter
using Inedo.Otter.Data;
#endif

namespace Inedo.Extensions.Operations.Otter
{
    internal sealed class ConfigurationStatusJsonModel
    {
        public ConfigurationStatusJsonModel()
        {
        }

#if Otter
        internal ConfigurationStatusJsonModel(Tables.Environments_Extended e)
        {
            this.Type = "environment";
            this.Name = e.Environment_Name;

            if (e.Aggregate_ServerConfiguration_Status_Code == Domains.ServerConflatedStatus.Current)
            {
                this.Status = "current";
            }
            else if (e.Descendants_Aggregate_ServerConfiguration_Status_Code == Domains.ServerConflatedStatus.Drifted)
            {
                this.Status = "drifted";
            }
            else
            {
                this.Status = "unknown";
            }
        }

        internal ConfigurationStatusJsonModel(Tables.ServerRoles_Extended r)
        {
            this.Type = "role";
            this.Name = r.ServerRole_Name;

            if (r.Aggregate_ServerConfiguration_Status_Code == Domains.ServerConflatedStatus.Current)
            {
                this.Status = "current";
            }
            else if (r.AllDriftPendingRemediation_Indicator)
            {
                this.Status = "pendingRemediation";
            }
            else if (r.Aggregate_ServerConfiguration_Status_Code == Domains.ServerConflatedStatus.Drifted)
            {
                this.Status = "drifted";
            }
            else
            {
                this.Status = "unknown";
            }

            this.CollectionDate = r.Aggregate_Latest_CollectionRun_Start_Date?.SetKind(DateTimeKind.Utc);
            this.LatestCollectionId = r.Aggregate_Latest_CollectionRun_Execution_Id;
        }

        internal ConfigurationStatusJsonModel(Tables.Servers_Extended s)
        {
            this.Type = "server";
            this.Name = s.Server_Name;

            if (!s.Active_Indicator)
            {
                this.Status = "disabled";
            }
            else if (s.Latest_CollectionRun_Execution_Status == Domains.ExecutionStatus.Error)
            {
                this.Status = "error";
                this.ErrorText = "The latest collection run execution failed.";
            }
            else if (s.Latest_CollectionRun_Execution_RunState == Domains.ExecutionRunState.Executing)
            {
                this.Status = "executing";
            }
            else if (s.ServerConfiguration_Status_Code == Domains.ServerConflatedStatus.Current)
            {
                this.Status = "current";
            }
            else if (s.AllDriftPendingRemediation_Indicator)
            {
                this.Status = "pendingRemediation";
                this.RemediationDate = s.FinalRemediation_Job_Start_Date?.SetKind(DateTimeKind.Utc);
                this.RemediationId = s.FinalRemediation_Job_Id;
            }
            else if (s.ServerConfiguration_Status_Code == Domains.ServerConflatedStatus.Drifted)
            {
                this.Status = "drifted";
            }
            else if (s.ServerStatus_Code == Domains.ServerStatusCode.Error)
            {
                this.Status = "error";
                this.ErrorText = "The agent on this server is in error status.";
            }
            else
            {
                this.Status = "unknown";
            }

            this.CollectionDate = s.Latest_CollectionRun_Start_Date?.SetKind(DateTimeKind.Utc);
            this.LatestCollectionId = s.Latest_CollectionRun_Execution_Id;
        }
#endif

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("errorText", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorText { get; set; }

        [JsonProperty("collectionDate")]
        public DateTime? CollectionDate { get; set; }

        [JsonProperty("latestCollectionId")]
        public int? LatestCollectionId { get; set; }

        [JsonProperty("remediationDate", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? RemediationDate { get; set; }

        [JsonProperty("remediationId", NullValueHandling = NullValueHandling.Ignore)]
        public int? RemediationId { get; set; }
    }
}
