// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET9_0_OR_GREATER && !SYSTEM_TEXT_JSON_V9
using System;
using System.ComponentModel;

namespace JsonSchemaMapper;

[Flags]
[EditorBrowsable(EditorBrowsableState.Never)]
internal enum JsonSchemaType
{
    Any = 0, // No type declared on the schema
    Null = 1,
    Boolean = 2,
    Integer = 4,
    Number = 8,
    String = 16,
    Array = 32,
    Object = 64,
}
#endif