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
using System.Collections.Generic;

namespace Tuvi.Core.Entities
{
    public class CoreException : Exception
    {
        public CoreException()
        {
        }

        public CoreException(string message) : base(message)
        {
        }

        public CoreException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NewMessagesCheckFailedException : CoreException
    {
        public List<EmailFolderError> ErrorsCollection { get; } = new List<EmailFolderError>();

        public NewMessagesCheckFailedException()
        {
        }

        public NewMessagesCheckFailedException(IEnumerable<EmailFolderError> errorsCollection)
        {
            if (errorsCollection != null)
            {
                ErrorsCollection.AddRange(errorsCollection);
            }
        }

        public NewMessagesCheckFailedException(string message) : base(message)
        {
        }

        public NewMessagesCheckFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public NewMessagesCheckFailedException(IEnumerable<EmailFolderError> errorsCollection, Exception innerException) : base(string.Empty, innerException)
        {
            if (errorsCollection != null)
            {
                ErrorsCollection.AddRange(errorsCollection);
            }
        }
    }

    public class NoPublicKeyException : CoreException
    {
        public EmailAddress Email { get; }

        public NoPublicKeyException(EmailAddress email, string message) : base(message)
        {
            Email = email;
        }

        public NoPublicKeyException(EmailAddress email, Exception innerException) : base(string.Empty, innerException)
        {
            Email = email;
        }

        private NoPublicKeyException()
        {
        }

        private NoPublicKeyException(string message) : base(message)
        {
        }

        private NoPublicKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
