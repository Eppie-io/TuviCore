// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2026 Eppie (https://eppie.io)                                    //
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

namespace Tuvi.Core.Entities.Exceptions
{
    public class BackupException : Exception
    {
        public BackupException()
        {
        }

        public BackupException(string message) : base(message)
        {
        }

        public BackupException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupParsingException : BackupException
    {
        public BackupParsingException()
        {
        }

        public BackupParsingException(string message) : base(message)
        {
        }

        public BackupParsingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NotBackupPackageException : BackupParsingException
    {
        public NotBackupPackageException()
        {
        }

        public NotBackupPackageException(string message) : base(message)
        {
        }

        public NotBackupPackageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class UnknownBackupProtectionException : BackupParsingException
    {
        public UnknownBackupProtectionException()
        {
        }

        public UnknownBackupProtectionException(string message) : base(message)
        {
        }

        public UnknownBackupProtectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupDeserializationException : BackupException
    {
        public BackupDeserializationException()
        {
        }

        public BackupDeserializationException(string message) : base(message)
        {
        }

        public BackupDeserializationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupBuildingException : BackupException
    {
        public BackupBuildingException()
        {
        }

        public BackupBuildingException(string message) : base(message)
        {
        }

        public BackupBuildingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupSerializationException : BackupException
    {
        public BackupSerializationException()
        {
        }

        public BackupSerializationException(string message) : base(message)
        {
        }

        public BackupSerializationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BackupVersionMismatchException : BackupException
    {
        public BackupVersionMismatchException()
        {
        }

        public BackupVersionMismatchException(string message) : base(message)
        {
        }

        public BackupVersionMismatchException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
