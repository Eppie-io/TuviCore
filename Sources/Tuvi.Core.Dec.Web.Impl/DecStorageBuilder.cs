using System;
using Tuvi.Core.Dec.Web.Impl;

namespace Tuvi.Core.Dec
{
    public static class DecStorageBuilder
    {
        public static IDecStorageClient CreateAzureClient(string url)
        {
            return new WebDecStorageClient(url);
        }

        public static IDecStorageClient CreateAzureClient(System.Uri url)
        {
            if (url is null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            return CreateAzureClient(url.AbsoluteUri);
        }
    }
}
