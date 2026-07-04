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
// (constants extracted from UnicodeTrieBuilder.cs, itself a port of
// https://github.com/foliojs/unicode-trie which derives from ICU's UTrie2)
// See the Third-Party Code section of the SimpleTinyPDF README for details.

namespace SimpleTinyPDF.Text
{
    /// <summary>
    /// Structural constants of the serialized UTrie2 format used by
    /// the BidiClasses.trie embedded resource.
    /// </summary>
    internal static class UnicodeTrieConstants
    {
        /// <summary>Shift size for getting the index-1 table offset.</summary>
        internal const int SHIFT_1 = 6 + 5;

        /// <summary>Shift size for getting the index-2 table offset.</summary>
        internal const int SHIFT_2 = 5;

        /// <summary>Difference between the two shift sizes.</summary>
        const int SHIFT_1_2 = SHIFT_1 - SHIFT_2;

        /// <summary>The part of the index-2 table for U+D800..U+DBFF stores values for lead surrogate code units.</summary>
        internal const int OMITTED_BMP_INDEX_1_LENGTH = 0x10000 >> SHIFT_1;

        /// <summary>Number of entries in an index-2 block.</summary>
        const int INDEX_2_BLOCK_LENGTH = 1 << SHIFT_1_2;

        /// <summary>Mask for getting the lower bits for the in-index-2-block offset.</summary>
        internal const int INDEX_2_MASK = INDEX_2_BLOCK_LENGTH - 1;

        /// <summary>Number of entries in a data block.</summary>
        const int DATA_BLOCK_LENGTH = 1 << SHIFT_2;

        /// <summary>Mask for getting the lower bits for the in-data-block offset.</summary>
        internal const int DATA_MASK = DATA_BLOCK_LENGTH - 1;

        /// <summary>Shift size for shifting left the index array values.</summary>
        internal const int INDEX_SHIFT = 2;

        /// <summary>The alignment size of a data block. Also the granularity for compaction.</summary>
        internal const int DATA_GRANULARITY = 1 << INDEX_SHIFT;

        /// <summary>The BMP part of the index-2 table is fixed and linear and starts at offset 0.</summary>
        internal const int LSCP_INDEX_2_OFFSET = 0x10000 >> SHIFT_2;

        const int LSCP_INDEX_2_LENGTH = 0x400 >> SHIFT_2;

        /// <summary>Count the lengths of both BMP pieces.</summary>
        const int INDEX_2_BMP_LENGTH = LSCP_INDEX_2_OFFSET + LSCP_INDEX_2_LENGTH;

        /// <summary>The 2-byte UTF-8 version of the index-2 table follows at offset 2080=0x820.</summary>
        const int UTF8_2B_INDEX_2_OFFSET = INDEX_2_BMP_LENGTH;

        /// <summary>The 2-byte UTF-8 version of the index-2 table only handles U+0000..U+07FF.</summary>
        const int UTF8_2B_INDEX_2_LENGTH = 0x800 >> 6;

        /// <summary>The index-1 table, only used for supplementary code points, at offset 2112=0x840.</summary>
        internal const int INDEX_1_OFFSET = UTF8_2B_INDEX_2_OFFSET + UTF8_2B_INDEX_2_LENGTH;
    }
}
