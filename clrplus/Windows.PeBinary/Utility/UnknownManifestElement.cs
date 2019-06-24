//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors.
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License.
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Windows.PeBinary.Utility {
    using System.Xml.Linq;

    public class UnknownManifestElement : ManifestException {
        public XElement Element;

        public UnknownManifestElement(string message, XElement element)
            : base(message) {
            Element = element;
        }
    }
}