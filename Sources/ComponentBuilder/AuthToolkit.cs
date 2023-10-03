namespace ComponentBuilder
{
    // ToDo: Auth-old
    //internal class AuthToolkit : IAuthToolkit
    //{
    //    private Func<Uri, Task<bool>> Launcher { get; set; }

    //    internal AuthToolkit(Func<Uri, Task<bool>> launcher)
    //    {
    //        Launcher = launcher;
    //    }

    //    public Task<bool> LaunchUriAsync(Uri uri)
    //    {
    //        return Launcher(uri);
    //    }

    //    public NameValueCollection ParseQuery(Uri uri)
    //    {
    //        try
    //        {
    //            return System.Web.HttpUtility.ParseQueryString(uri?.Query);
    //        }
    //        catch(ArgumentNullException)
    //        {
    //            return new NameValueCollection();
    //        }
    //    }

    //    public NameValueCollection ParseQueryString(string query)
    //    {
    //        try
    //        {
    //            return System.Web.HttpUtility.ParseQueryString(query);
    //        }
    //        catch (ArgumentNullException)
    //        {
    //            return new NameValueCollection();
    //        }
    //    }

    //    public Uri ConvertToUri(string str)
    //    {
    //        try
    //        {
    //            var builder = new UriBuilder(str);
    //            return builder.Uri;
    //        }
    //        catch (UriFormatException)
    //        {
    //            return null;
    //        }
    //    }
    //}
}
