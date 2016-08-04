using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.BuildMaster.Extensibility.Actions.HTTP;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.ExecutionEngine.Variables;
using Inedo.Extensions.Operations.HTTP;
using Inedo.IO;

namespace Inedo.Extensions.Legacy.ActionImporters
{
    internal static class HTTP
    {
        public sealed class Get : IActionOperationConverter<HttpGetAction, HttpGetOperation>
        {
            public ConvertedOperation<HttpGetOperation> ConvertActionToOperation(HttpGetAction action, IActionConverterContext context)
            {
                GetHttpMethod method;
                if (!Enum.TryParse(context.ConvertLegacyExpression(action.HttpMethod), true, out method))
                    method = GetHttpMethod.GET;

                return new HttpGetOperation
                {
                    Url = context.ConvertLegacyExpression(action.Url),
                    Method = method,
                    ErrorStatusCodes = context.ConvertLegacyExpression(action.ErrorStatusCodes),
                    LogResponseBody = action.LogResponseBody,
                    ResponseBodyVariable = action.SaveResponseBodyAsVariable ? HttpActionBase.HttpResponseBodyVariableName : null
                };
            }
        }

        public sealed class Post : IActionOperationConverter<HttpPostAction, HttpPostOperation>
        {
            public ConvertedOperation<HttpPostOperation> ConvertActionToOperation(HttpPostAction action, IActionConverterContext context)
            {
                PostHttpMethod method;
                if (!Enum.TryParse(context.ConvertLegacyExpression(action.HttpMethod), true, out method))
                    method = PostHttpMethod.POST;

                return new ConvertedOperation<HttpPostOperation>(
                    new HttpPostOperation
                    {
                        Url = context.ConvertLegacyExpression(action.Url),
                        Method = method,
                        ErrorStatusCodes = context.ConvertLegacyExpression(action.ErrorStatusCodes),
                        LogResponseBody = action.LogResponseBody,
                        ResponseBodyVariable = action.SaveResponseBodyAsVariable ? HttpActionBase.HttpResponseBodyVariableName : null,
                        ContentType = context.ConvertLegacyExpression(action.ContentType),
                        LogRequestData = action.LogRequestData,
                        PostData = AH.NullIf(context.ConvertLegacyExpression(action.PostData), string.Empty)
                    }
                )
                {
                    [nameof(HttpPostOperation.FormData)] = ConvertFormData(action.FormData, context)
                };
            }
        }

        public sealed class Upload : IActionOperationConverter<HttpFileUploadAction, HttpFileUploadOperation>
        {
            public ConvertedOperation<HttpFileUploadOperation> ConvertActionToOperation(HttpFileUploadAction action, IActionConverterContext context)
            {
                var fileName = action.FileName;
                if (!string.IsNullOrEmpty(action.OverriddenSourceDirectory))
                    fileName = PathEx.Combine(action.OverriddenSourceDirectory, fileName);

                return new HttpFileUploadOperation
                {
                    Url = context.ConvertLegacyExpression(action.Url),
                    ErrorStatusCodes = context.ConvertLegacyExpression(action.ErrorStatusCodes),
                    LogResponseBody = action.LogResponseBody,
                    ResponseBodyVariable = action.SaveResponseBodyAsVariable ? HttpActionBase.HttpResponseBodyVariableName : null,
                    FileName = context.ConvertLegacyExpression(fileName)
                };
            }
        }

        private static string ConvertFormData(IEnumerable<KeyValuePair<string, string>> data, IActionConverterContext context)
        {
            var list = data?.ToList();

            if (list == null || list.Count == 0)
                return null;

            var dictionary = new Dictionary<string, string>();
            foreach (var item in list)
                dictionary[item.Key] = context.ConvertLegacyExpression(item.Value);

            return new ProcessedString(new MapTextValue(dictionary)).ToString();
        }
    }
}
