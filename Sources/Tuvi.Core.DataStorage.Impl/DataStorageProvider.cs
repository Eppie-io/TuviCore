using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Impl
{
    public static class DataStorageProvider
    {

        /// <summary>
        /// Create data base to store accounts info and messages 
        /// </summary>
        /// <param name="path">Path to database file</param>
        /// <exception cref="DataBaseException"
        /// <returns>DataStorage</returns>
        public static IDataStorage GetDataStorage(string path)
        {
            var db = new DataStorage(path);
            return db;
        }
    }
}
