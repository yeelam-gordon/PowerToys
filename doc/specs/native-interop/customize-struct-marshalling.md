---

title: Customizing structure marshalling - .NET

description: Learn how to customize how .NET marshals structures to a native representation.

ms.date: 01/18/2019

dev\_langs:

&nbsp; - "csharp"

&nbsp; - "cpp"

ms.topic: how-to

---

\# Customize structure marshalling



Sometimes the default marshalling rules for structures aren't exactly what you need. The .NET runtimes provide a few extension points for you to customize your structure's layout and how fields are marshalled. Customizing structure layout is supported for all scenarios, but customizing field marshalling is only supported for scenarios where runtime marshalling is enabled. If \[runtime marshalling is disabled](disabled-marshalling.md), then any field marshalling must be done manually.



> \[!NOTE]

> This article doesn't cover customizing marshalling for source-generated interop. If you're using \[source-generated interop for P/Invokes](./pinvoke-source-generation.md) or \[COM](./comwrappers-source-generation.md), see \[customizing marshalling](./custom-marshalling-source-generation.md).



\## Customize structure layout



.NET provides the <xref:System.Runtime.InteropServices.StructLayoutAttribute?displayProperty=nameWithType> attribute and the <xref:System.Runtime.InteropServices.LayoutKind?displayProperty=nameWithType> enumeration to allow you to customize how fields are placed in memory. The following guidance will help you avoid common issues.



✔️ CONSIDER using `LayoutKind.Sequential` whenever possible.



✔️ DO only use `LayoutKind.Explicit` in marshalling when your native struct also has an explicit layout, such as a union.



❌ AVOID using classes to express complex native types through inheritance.



❌ AVOID using `LayoutKind.Explicit` when marshalling structures on non-Windows platforms if you need to target runtimes before .NET Core 3.0. The .NET Core runtime before 3.0 doesn't support passing explicit structures by value to native functions on Intel or AMD 64-bit non-Windows systems. However, the runtime supports passing explicit structures by reference on all platforms.



\## Customizing Boolean field marshalling



Native code has many different Boolean representations. On Windows alone, there are three ways to represent Boolean values. The runtime doesn't know the native definition of your structure, so the best it can do is make a guess on how to marshal your Boolean values. The .NET runtime provides a way to indicate how to marshal your Boolean field. The following examples show how to marshal .NET `bool` to different native Boolean types.



Boolean values default to marshalling as a native 4-byte Win32 \[`BOOL`](/windows/desktop/winprog/windows-data-types#BOOL) value as shown in the following example:



```csharp

public struct WinBool

{

&nbsp;   public bool b;

}

```



```cpp

struct WinBool

{

&nbsp;   public BOOL b;

};

```



If you want to be explicit, you can use the <xref:System.Runtime.InteropServices.UnmanagedType.Bool?displayProperty=nameWithType> value to get the same behavior as above:



```csharp

public struct WinBool

{

&nbsp;   \[MarshalAs(UnmanagedType.Bool)]

&nbsp;   public bool b;

}

```



```cpp

struct WinBool

{

&nbsp;   public BOOL b;

};

```



Using the `UnmanagedType.U1` or `UnmanagedType.I1` values below, you can tell the runtime to marshal the `b` field as a 1-byte native `bool` type.



```csharp

public struct CBool

{

&nbsp;   \[MarshalAs(UnmanagedType.U1)]

&nbsp;   public bool b;

}

```



```cpp

struct CBool

{

&nbsp;   public bool b;

};

```



On Windows, you can use the <xref:System.Runtime.InteropServices.UnmanagedType.VariantBool?displayProperty=nameWithType> value to tell the runtime to marshal your Boolean value to a 2-byte `VARIANT\_BOOL` value:



```csharp

public struct VariantBool

{

&nbsp;   \[MarshalAs(UnmanagedType.VariantBool)]

&nbsp;   public bool b;

}

```



```cpp

struct VariantBool

{

&nbsp;   public VARIANT\_BOOL b;

};

```



> \[!NOTE]

> `VARIANT\_BOOL` is different than most bool types in that `VARIANT\_TRUE = -1` and `VARIANT\_FALSE = 0`. Additionally, all values that aren't equal to `VARIANT\_TRUE` are considered false.



\## Customizing array field marshalling



.NET also includes a few ways to customize array marshalling.



By default, .NET marshals arrays as a pointer to a contiguous list of the elements:



```csharp

public struct DefaultArray

{

&nbsp;   public int\[] values;

}

```



```cpp

struct DefaultArray

{

&nbsp;   int32\_t\* values;

};

```



If you're interfacing with COM APIs, you may have to marshal arrays as `SAFEARRAY\*` objects. You can use the <xref:System.Runtime.InteropServices.MarshalAsAttribute?displayProperty=nameWithType> and the <xref:System.Runtime.InteropServices.UnmanagedType.SafeArray?displayProperty=nameWithType> value to tell the runtime to marshal an array as a `SAFEARRAY\*`:



```csharp

public struct SafeArrayExample

{

&nbsp;   \[MarshalAs(UnmanagedType.SafeArray)]

&nbsp;   public int\[] values;

}

```



```cpp

struct SafeArrayExample

{

&nbsp;   SAFEARRAY\* values;

};

```



If you need to customize what type of element is in the `SAFEARRAY`, then you can use the <xref:System.Runtime.InteropServices.MarshalAsAttribute.SafeArraySubType?displayProperty=nameWithType> and <xref:System.Runtime.InteropServices.MarshalAsAttribute.SafeArrayUserDefinedSubType?displayProperty=nameWithType> fields to customize the exact element type of the `SAFEARRAY`.



If you need to marshal the array in-place, you can use the <xref:System.Runtime.InteropServices.UnmanagedType.ByValArray?displayProperty=nameWithType> value to tell the marshaller to marshal the array in-place. When you're using this marshalling, you also must supply a value to the <xref:System.Runtime.InteropServices.MarshalAsAttribute.SizeConst?displayProperty=nameWithType> field  for the number of elements in the array so the runtime can correctly allocate space for the structure.



```csharp

public struct InPlaceArray

{

&nbsp;   \[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]

&nbsp;   public int\[] values;

}

```



```cpp

struct InPlaceArray

{

&nbsp;   int values\[4];

};

```



> \[!NOTE]

> .NET doesn't support marshalling a variable length array field as a C99 Flexible Array Member.



\## Customizing string field marshalling



.NET also provides a wide variety of customizations for marshalling string fields.



By default, .NET marshals a string as a pointer to a null-terminated string. The encoding depends on the value of the <xref:System.Runtime.InteropServices.StructLayoutAttribute.CharSet?displayProperty=nameWithType> field in the <xref:System.Runtime.InteropServices.StructLayoutAttribute?displayProperty=nameWithType>. If no attribute is specified, the encoding defaults to an ANSI encoding.



```csharp

\[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]

public struct DefaultString

{

&nbsp;   public string str;

}

```



```cpp

struct DefaultString

{

&nbsp;   char\* str;

};

```



```csharp

\[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]

public struct DefaultString

{

&nbsp;   public string str;

}

```



```cpp

struct DefaultString

{

&nbsp;   char16\_t\* str; // Could also be wchar\_t\* on Windows.

};

```



If you need to use different encodings for different fields or just prefer to be more explicit in your struct definition, you can use the <xref:System.Runtime.InteropServices.UnmanagedType.LPStr?displayProperty=nameWithType> or <xref:System.Runtime.InteropServices.UnmanagedType.LPWStr?displayProperty=nameWithType> values on a <xref:System.Runtime.InteropServices.MarshalAsAttribute?displayProperty=nameWithType> attribute.



```csharp

public struct AnsiString

{

&nbsp;   \[MarshalAs(UnmanagedType.LPStr)]

&nbsp;   public string str;

}

```



```cpp

struct AnsiString

{

&nbsp;   char\* str;

};

```



```csharp

public struct UnicodeString

{

&nbsp;   \[MarshalAs(UnmanagedType.LPWStr)]

&nbsp;   public string str;

}

```



```cpp

struct UnicodeString

{

&nbsp;   char16\_t\* str; // Could also be wchar\_t\* on Windows.

};

```



If you want to marshal your strings using the UTF-8 encoding, you can use the <xref:System.Runtime.InteropServices.UnmanagedType.LPUTF8Str?displayProperty=nameWithType> value in your <xref:System.Runtime.InteropServices.MarshalAsAttribute>.



```csharp

public struct UTF8String

{

&nbsp;   \[MarshalAs(UnmanagedType.LPUTF8Str)]

&nbsp;   public string str;

}

```



```cpp

struct UTF8String

{

&nbsp;   char\* str;

};

```



> \[!NOTE]

> Using <xref:System.Runtime.InteropServices.UnmanagedType.LPUTF8Str?displayProperty=nameWithType> requires either .NET Framework 4.7 (or later versions) or .NET Core 1.1 (or later versions). It isn't available in .NET Standard 2.0.



If you're working with COM APIs, you may need to marshal a string as a `BSTR`. Using the <xref:System.Runtime.InteropServices.UnmanagedType.BStr?displayProperty=nameWithType> value, you can marshal a string as a `BSTR`.



```csharp

public struct BString

{

&nbsp;   \[MarshalAs(UnmanagedType.BStr)]

&nbsp;   public string str;

}

```



```cpp

struct BString

{

&nbsp;   BSTR str;

};

```



When using a WinRT-based API, you may need to marshal a string as an `HSTRING`. Using the <xref:System.Runtime.InteropServices.UnmanagedType.HString?displayProperty=nameWithType> value, you can marshal a string as a `HSTRING`. `HSTRING` marshalling is only supported on runtimes with built-in WinRT support. WinRT support was \[removed in .NET 5](../../core/compatibility/interop/5.0/built-in-support-for-winrt-removed.md), so `HSTRING` marshalling is not supported in .NET 5 or newer.



```csharp

public struct HString

{

&nbsp;   \[MarshalAs(UnmanagedType.HString)]

&nbsp;   public string str;

}

```



```cpp

struct BString

{

&nbsp;   HSTRING str;

};

```



If your API requires you to pass the string in-place in the structure, you can use the <xref:System.Runtime.InteropServices.UnmanagedType.ByValTStr?displayProperty=nameWithType> value. Do note that the encoding for a string marshalled by `ByValTStr` is determined from the `CharSet` attribute. Additionally, it requires that a string length is passed by the <xref:System.Runtime.InteropServices.MarshalAsAttribute.SizeConst?displayProperty=nameWithType> field.



```csharp

\[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]

public struct DefaultString

{

&nbsp;   \[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]

&nbsp;   public string str;

}

```



```cpp

struct DefaultString

{

&nbsp;   char str\[4];

};

```



```csharp

\[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]

public struct DefaultString

{

&nbsp;   \[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]

&nbsp;   public string str;

}

```



```cpp

struct DefaultString

{

&nbsp;   char16\_t str\[4]; // Could also be wchar\_t\[4] on Windows.

};

```



\## Customizing decimal field marshalling



If you're working on Windows, you might encounter some APIs that use the native \[`CY` or `CURRENCY`](/windows/win32/api/wtypes/ns-wtypes-cy-r1) structure. By default, the .NET `decimal` type marshals to the native \[`DECIMAL`](/windows/win32/api/wtypes/ns-wtypes-decimal-r1) structure. However, you can use a <xref:System.Runtime.InteropServices.MarshalAsAttribute> with the <xref:System.Runtime.InteropServices.UnmanagedType.Currency?displayProperty=nameWithType> value to instruct the marshaller to convert a `decimal` value to a native `CY` value.



```csharp

public struct Currency

{

&nbsp;   \[MarshalAs(UnmanagedType.Currency)]

&nbsp;   public decimal dec;

}

```



```cpp

struct Currency

{

&nbsp;   CY dec;

};

```



\### Unions



A union is a data type that can contain different types of data atop the same memory. It's a common form of data in the C language. A union can be expressed in .NET using `LayoutKind.Explicit`. It's recommended to use structs when defining a union in .NET. Using classes can cause layout issues and produce unpredictable behavior.



```cpp

struct device1\_config

{

&nbsp;   void\* a;

&nbsp;   void\* b;

&nbsp;   void\* c;

};

struct device2\_config

{

&nbsp;   int32\_t a;

&nbsp;   int32\_t b;

};

struct config

{

&nbsp;   int32\_t type;



&nbsp;   union

&nbsp;   {

&nbsp;       device1\_config dev1;

&nbsp;       device2\_config dev2;

&nbsp;   };

};

```



```csharp

public unsafe struct Device1Config

{

&nbsp;   void\* a;

&nbsp;   void\* b;

&nbsp;   void\* c;

}



public struct Device2Config

{

&nbsp;   int a;

&nbsp;   int b;

}



public struct Config

{

&nbsp;   public int Type;



&nbsp;   public \_Union Anonymous;



&nbsp;   \[StructLayout(LayoutKind.Explicit)]

&nbsp;   public struct \_Union

&nbsp;   {

&nbsp;       \[FieldOffset(0)]

&nbsp;       public Device1Config Dev1;



&nbsp;       \[FieldOffset(0)]

&nbsp;       public Device2Config Dev2;

&nbsp;   }

}

```



\## Marshal `System.Object`



On Windows, you can marshal `object`-typed fields to native code. You can marshal these fields to one of three types:



\- \[`VARIANT`](/windows/win32/api/oaidl/ns-oaidl-variant)

\- \[`IUnknown\*`](/windows/desktop/api/unknwn/nn-unknwn-iunknown)

\- \[`IDispatch\*`](/windows/desktop/api/oaidl/nn-oaidl-idispatch)



By default, an `object`-typed field will be marshalled to an `IUnknown\*` that wraps the object.



```csharp

public struct ObjectDefault

{

&nbsp;   public object obj;

}

```



```cpp

struct ObjectDefault

{

&nbsp;   IUnknown\* obj;

};

```



If you want to marshal an object field to an `IDispatch\*`, add a <xref:System.Runtime.InteropServices.MarshalAsAttribute> with the <xref:System.Runtime.InteropServices.UnmanagedType.IDispatch?displayProperty=nameWithType> value.



```csharp

public struct ObjectDispatch

{

&nbsp;   \[MarshalAs(UnmanagedType.IDispatch)]

&nbsp;   public object obj;

}

```



```cpp

struct ObjectDispatch

{

&nbsp;   IDispatch\* obj;

};

```



If you want to marshal it as a `VARIANT`, add a <xref:System.Runtime.InteropServices.MarshalAsAttribute> with the <xref:System.Runtime.InteropServices.UnmanagedType.Struct?displayProperty=nameWithType> value.



```csharp

public struct ObjectVariant

{

&nbsp;   \[MarshalAs(UnmanagedType.Struct)]

&nbsp;   public object obj;

}

```



```cpp

struct ObjectVariant

{

&nbsp;   VARIANT obj;

};

```



The following table describes how different runtime types of the `obj` field map to the various types stored in a `VARIANT`:



| .NET Type                                        | VARIANT Type  |

|--------------------------------------------------|---------------|

| `byte`                                           | `VT\_UI1`      |

| `sbyte`                                          | `VT\_I1`       |

| `short`                                          | `VT\_I2`       |

| `ushort`                                         | `VT\_UI2`      |

| `int`                                            | `VT\_I4`       |

| `uint`                                           | `VT\_UI4`      |

| `long`                                           | `VT\_I8`       |

| `ulong`                                          | `VT\_UI8`      |

| `float`                                          | `VT\_R4`       |

| `double`                                         | `VT\_R8`       |

| `char`                                           | `VT\_UI2`      |

| `string`                                         | `VT\_BSTR`     |

| `System.Runtime.InteropServices.BStrWrapper`     | `VT\_BSTR`     |

| `object`                                         | `VT\_DISPATCH` |

| `System.Runtime.InteropServices.UnknownWrapper`  | `VT\_UNKNOWN`  |

| `System.Runtime.InteropServices.DispatchWrapper` | `VT\_DISPATCH` |

| `System.Reflection.Missing`                      | `VT\_ERROR`    |

| `(object)null`                                   | `VT\_EMPTY`    |

| `bool`                                           | `VT\_BOOL`     |

| `System.DateTime`                                | `VT\_DATE`     |

| `decimal`                                        | `VT\_DECIMAL`  |

| `System.Runtime.InteropServices.CurrencyWrapper` | `VT\_CURRENCY` |

| `System.DBNull`                                  | `VT\_NULL`     |

