using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Inedo.BuildMaster.Data;
using Inedo.Serialization;

namespace Inedo.BuildMaster.Extensibility.Actions.HTTP
{
    /// <summary>
    /// Base class for HTTP actions.
    /// </summary>
    public abstract class HttpActionBase : RemoteActionBase, IMissingPersistentPropertyHandler
    {
        /// <summary>
        /// The maximum number of characters to log in a response.
        /// </summary>
        private const int MaxResponseLength = 5000;

        public const string HttpResponseBodyVariableName = "HttpResponseBody";

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpActionBase"/> class.
        /// </summary>
        protected HttpActionBase()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether to log the Content-Body of the response.
        /// </summary>
        [Persistent]
        public bool LogResponseBody { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether [save response body as variable].
        /// </summary>
        [Persistent]
        public bool SaveResponseBodyAsVariable { get; set; }
        /// <summary>
        /// Gets or sets a range of status codes that should be considered a failed response.
        /// </summary>
        [Persistent]
        public string ErrorStatusCodes { get; set; } = "400:599";
        /// <summary>
        /// Gets or sets the HTTP method used for the request.
        /// </summary>
        [Persistent]
        public string HttpMethod { get; set; }

        /// <summary>
        /// Makes the request and handles the response.
        /// </summary>
        /// <param name="request">The request.</param>
        protected void PerformRequest(HttpWebRequest request)
        {
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                this.ProcessResponse(response);
            }
            catch (WebException ex)
            {
                this.ProcessResponse((HttpWebResponse)ex.Response);
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        protected void ProcessResponse(HttpWebResponse response)
        {            
            string message = string.Format("Server responded with status code {0} - {1}.", (int)response.StatusCode, Util.CoalesceStr(response.StatusDescription, response.StatusCode));

            var errorCodeRanges = StatusCodeRangeList.Parse(this.ErrorStatusCodes);
            if (errorCodeRanges.IsInAnyRange((int)response.StatusCode))
                this.LogError(message);
            else
                this.LogInformation(message);            

            if (this.LogResponseBody || this.SaveResponseBodyAsVariable)
            {
                if (response.ContentLength == 0)
                {
                    this.LogDebug("The Content Length of the response was 0.");
                    return;
                }

                try
                {
                    string text = new StreamReader(response.GetResponseStream()).ReadToEnd();

                    if (this.SaveResponseBodyAsVariable)
                    {
                        this.LogDebug("Saving response body to ${0} variable...", HttpActionBase.HttpResponseBodyVariableName);
                        this.Context.Variables[HttpActionBase.HttpResponseBodyVariableName] = text;
                    }

                    if (this.LogResponseBody)
                    {
                        if (text.Length > MaxResponseLength)
                        {
                            text = text.Substring(0, MaxResponseLength);
                            this.LogDebug("The following response Content Body is truncated to {0} characters...", MaxResponseLength);
                        }

                        if (!string.IsNullOrEmpty(text))
                            this.LogInformation("Response Content Body: {0}", text);
                    }
                }
                catch (Exception ex)
                {
                    this.LogWarning("Could not read response Content Body because: {0}", ex.Message);
                }
            }
        }

        protected void SetResponseBodyVariable()
        {
            if (this.SaveResponseBodyAsVariable)
            {
                DB.Variables_CreateOrUpdateVariableDefinition(
                    Variable_Name: HttpResponseBodyVariableName,
                    Value_Text: this.Context.Variables[HttpResponseBodyVariableName],
                    Environment_Id: this.Context.EnvironmentId,
                    ServerRole_Id: null,
                    Application_Id: this.Context.ApplicationId,
                    Release_Number: this.Context.ReleaseNumber,
                    Build_Number: this.Context.BuildNumber,
                    Execution_Id: this.Context.ExecutionId,
                    ApplicationGroup_Id: null,
                    Deployable_Id: null,
                    Promotion_Id: null,
                    Server_Id: null,
                    Sensitive_Indicator: false
                );
            }
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            var value = missingProperties.GetValueOrDefault("ErrorIfBadStatus");
            if (value != null && string.IsNullOrEmpty(this.ErrorStatusCodes) && Util.Bool.ParseF(value))
            {
                this.ErrorStatusCodes = "400:599";
            }
        }
    }
}
