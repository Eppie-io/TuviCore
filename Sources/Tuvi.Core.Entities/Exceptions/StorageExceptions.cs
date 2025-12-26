// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

using System;

namespace Tuvi.Core.Entities
{
    public class DataBaseException : Exception
    {
        public DataBaseException()
        {
        }

        public DataBaseException(string message)
            : base(message)
        {
        }

        public DataBaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class DataBasePasswordException : DataBaseException
    {
        public DataBasePasswordException()
        {
        }

        public DataBasePasswordException(string message)
            : base(message)
        {
        }

        public DataBasePasswordException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class DataBaseAlreadyExistsException : DataBaseException
    {
        public DataBaseAlreadyExistsException()
        {
        }

        public DataBaseAlreadyExistsException(string message)
            : base(message)
        {
        }

        public DataBaseAlreadyExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class DataBaseNotCreatedException : DataBaseException
    {
        public DataBaseNotCreatedException()
        {
        }

        public DataBaseNotCreatedException(string message)
            : base(message)
        {
        }

        public DataBaseNotCreatedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class NoCollectionException : DataBaseException
    {
        public NoCollectionException()
        {
        }

        public NoCollectionException(string message) : base(message)
        {
        }

        public NoCollectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DataBaseMigrationException : DataBaseException
    {
        public DataBaseMigrationException()
        {
        }

        public DataBaseMigrationException(string message) : base(message)
        {
        }

        public DataBaseMigrationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
