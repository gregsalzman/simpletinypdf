// RichTextKit
// Copyright © 2019-2020 Topten Software. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may
// not use this product except in compliance with the License. You may obtain
// a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations
// under the License.
//
// This file was adapted for SimpleTinyPDF from RichTextKit
// https://github.com/toptensoftware/RichTextKit (Apache License 2.0)
// (trimmed to the bidi-related lookups only; the BidiClasses.trie embedded
// resource is taken verbatim from RichTextKit and encodes, per code point,
// the bidi class, paired bracket type and paired bracket partner generated
// from the Unicode Character Database.)
// See the Third-Party Code section of the SimpleTinyPDF README for details.

namespace SimpleTinyPDF.Text
{
    /// <summary>
    /// Helper for looking up Unicode bidi character class information
    /// </summary>
    internal static class UnicodeClasses
    {
        static UnicodeClasses()
        {
            _bidiTrie = new UnicodeTrie(typeof(UnicodeClasses).Assembly.GetManifestResourceStream("SimpleTinyPDF.Text.Resources.BidiClasses.trie"));
        }

        static UnicodeTrie _bidiTrie;

        /// <summary>
        /// Get the directionality of a Unicode Code Point
        /// </summary>
        /// <param name="codePoint">The code point in question</param>
        /// <returns>The code point's directionality</returns>
        public static Directionality Directionality(int codePoint)
        {
            return (Directionality)(_bidiTrie.Get(codePoint) >> 24);
        }

        /// <summary>
        /// Get the packed bidi data of a Unicode Code Point
        /// (directionality, paired bracket type and paired bracket partner)
        /// </summary>
        /// <param name="codePoint">The code point in question</param>
        /// <returns>The code point's packed bidi data</returns>
        public static uint BidiData(int codePoint)
        {
            return _bidiTrie.Get(codePoint);
        }

        /// <summary>
        /// Get the bracket type for a Unicode Code Point
        /// </summary>
        /// <param name="codePoint">The code point in question</param>
        /// <returns>The code point's paired bracket type</returns>
        public static PairedBracketType PairedBracketType(int codePoint)
        {
            return (PairedBracketType)((_bidiTrie.Get(codePoint) >> 16) & 0xFF);
        }

        /// <summary>
        /// Get the associated bracket type for a Unicode Code Point
        /// </summary>
        /// <param name="codePoint">The code point in question</param>
        /// <returns>The code point's opposite bracket, or 0 if not a bracket</returns>
        public static int AssociatedBracket(int codePoint)
        {
            return (int)(_bidiTrie.Get(codePoint) & 0xFFFF);
        }
    }
}
