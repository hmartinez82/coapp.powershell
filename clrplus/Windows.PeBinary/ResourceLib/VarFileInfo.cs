//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     ResourceLib Original Code from http://resourcelib.codeplex.com
//     Original Copyright (c) 2008-2009 Vestris Inc.
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
// MIT License
// You may freely use and distribute this software under the terms of the following license agreement.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of 
// the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO 
// THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Windows.PeBinary.ResourceLib {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Api.Enumerations;
    using Core.Collections;

    /// <summary>
    ///     This structure depicts the organization of data in a file-version resource.
    ///     It contains version information not dependent on a particular language and code page combination.
    ///     http://msdn.microsoft.com/en-us/library/aa909193.aspx
    /// </summary>
    public class VarFileInfo : ResourceTableHeader {
        private IDictionary<string, VarTable> _vars = new XDictionary<string, VarTable>();

        /// <summary>
        ///     A new hardware independent dictionary of language and code page identifier tables.
        /// </summary>
        public VarFileInfo() : base("VarFileInfo") {
            _header.wType = (UInt16)ResourceHeaderType.StringData;
        }

        /// <summary>
        ///     An existing hardware independent dictionary of language and code page identifier tables.
        /// </summary>
        /// <param name="lpRes">Pointer to the beginning of data.</param>
        internal VarFileInfo(IntPtr lpRes) {
            Read(lpRes);
        }

        /// <summary>
        ///     A hardware independent dictionary of language and code page identifier tables.
        /// </summary>
        public IDictionary<string, VarTable> Vars {
            get {
                return _vars;
            }
        }

        /// <summary>
        ///     The default language and code page identifier table.
        /// </summary>
        public VarTable Default {
            get {
                var varsEnum = _vars.GetEnumerator();
                if (varsEnum.MoveNext()) {
                    return varsEnum.Current.Value;
                }
                return null;
            }
        }

        /// <summary>
        ///     Returns a language and code page identifier table.
        /// </summary>
        /// <param name="language">Language ID.</param>
        /// <returns>A language and code page identifier table.</returns>
        public UInt16 this[UInt16 language] {
            get {
                return Default[language];
            }
            set {
                Default[language] = value;
            }
        }

        /// <summary>
        ///     Read a hardware independent dictionary of language and code page identifier tables.
        /// </summary>
        /// <param name="lpRes">Pointer to the beginning of data.</param>
        /// <returns>Pointer to the end of data.</returns>
        internal override IntPtr Read(IntPtr lpRes) {
            _vars.Clear();
            var pChild = base.Read(lpRes);

            while (pChild.ToInt32() < (lpRes.ToInt32() + _header.wLength)) {
                var res = new VarTable(pChild);
                _vars.Add(res.Key, res);
                pChild = ResourceUtil.Align(pChild.ToInt32() + res.Header.wLength);
            }

            return new IntPtr(lpRes.ToInt32() + _header.wLength);
        }

        /// <summary>
        ///     Write the hardware independent dictionary of language and code page identifier tables to a binary stream.
        /// </summary>
        /// <param name="w">Binary stream.</param>
        internal override void Write(BinaryWriter w) {
            var headerPos = w.BaseStream.Position;
            base.Write(w);

            var varsEnum = _vars.GetEnumerator();
            while (varsEnum.MoveNext()) {
                varsEnum.Current.Value.Write(w);
            }

            ResourceUtil.WriteAt(w, w.BaseStream.Position - headerPos, headerPos);
        }

        /// <summary>
        ///     String representation of VarFileInfo.
        /// </summary>
        /// <param name="indent">Indent.</param>
        /// <returns>String in the VarFileInfo format.</returns>
        public override string ToString(int indent) {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("{0}BEGIN", new String(' ', indent)));
            foreach (var var in _vars.Values) {
                sb.Append(var.ToString(indent + 1));
            }
            sb.AppendLine(string.Format("{0}END", new String(' ', indent)));
            return sb.ToString();
        }
    }
}