using System;
using System.Collections.Generic;
using System.Linq;
using IKVM.Reflection;
using Type = IKVM.Reflection.Type;

namespace Embeddinator.ObjC {
	public static class NameGenerator {
		public static Dictionary<string, string> ObjCTypeToArgument = new Dictionary<string, string> {
			{ "int", "anInt" },
			{ "unsigned int", "aUint" },
			{ "double", "aDouble" },
			{ "float", "aFloat" },
			{ "NSString", "aString" },
			{ "NSString *", "aString" },
			{ "id", "anObject" },
			{ "NSObject", "anObject" },
			{ "NSPoint", "aPoint" },
			{ "NSRect", "aRect" },
			{ "NSFont", "fontObj" },
			{ "SEL", "aSelector" },
			{ "short", "aShort" },
			{ "unsigned short", "aUshort" },
			{ "long long", "aLong" },
			{ "unsigned long long", "aUlong" },
			{ "bool", "aBool" },
			{ "char", "aChar" },
			{ "unsigned char", "aChar" },
			{ "signed char", "aChar" }
		};

		public static Dictionary<string, string> ObjCTypeToMethodName = new Dictionary<string, string> {
			{ "int", "IntValue" },
			{ "unsigned int", "UIntValue" },
			{ "double", "DoubleValue" },
			{ "float", "FloatValue" },
			{ "NSString *", "StringValue" },
			{ "NSObject", "ObjectValue" },
			{ "NSPoint", "PointValue" },
			{ "NSRect", "RectValue" },
			{ "NSFont", "FontValue" },
			{ "SEL", "SelectorValue" },
			{ "short", "ShortValue" },
			{ "unsigned short", "UShortValue" },
			{ "long long", "LongValue" },
			{ "unsigned long long", "ULongValue" },
			{ "bool", "BoolValue" },
			{ "char", "CharValue" },
			{ "unsigned char", "UCharValue" },
			{ "signed char", "SCharValue" }
		};

		public static string GetObjCName (Type t)
		{
			if (t.IsClass) {
				string name = null;
				var delayed = new List<Exception> ();

				var attrib = t.GetCustomAttributesData ().SingleOrDefault (a => a.AttributeType.Name.Equals ("RegisterAttribute"));
				if (attrib != null) {
					var argsCount = attrib.ConstructorArguments.Count;
					switch (argsCount) {
					// case 0 is considered invalid for use here, so handle it the same as the default case.
					case 1:
					case 2: {
							// Since we aren't enforcing RegisterAttribute come from 
							// a Xamarin-provided assembly, check to ensure the RA we
							// found conforms to our expectations.
							var arg = attrib.ConstructorArguments [0].ArgumentType;
							if (Type.GetTypeCode (arg) == TypeCode.String) {
								name = (string)attrib.ConstructorArguments [0].Value;
							} else {
								delayed.Add (ErrorHelper.CreateWarning (1060, $"Invalid RegisterAttribute found on class '{t.FullName}'. Expected the first constructor parameter to be of type `string` but found type `{arg.FullName}` instead."));
							}
							break;
						}
					default:
						delayed.Add (ErrorHelper.CreateWarning (1061, $"Invalid RegisterAttribute found on class '{t.FullName}'. Expected a constructor with either 1 or 2 parameters but found {argsCount} instead."));
						break;
					}
					// Handle cases where some used named properties.
					// If they mixed both positional and named params,
					// then assume the more specific one (the named)
					// parameter is correct, and issue a warning.
					if (attrib.NamedArguments.Any (a => !a.IsField)) {
						foreach (var prop in attrib.NamedArguments) {
							var propName = prop.MemberName;
							switch (propName) {
							case "IsWrapper":
								continue; // Intentionally ignore.
							case "Name":
								if (string.IsNullOrWhiteSpace (name)) {
									name = (string)prop.TypedValue.Value;
								} else {
									var propVal = (string)prop.TypedValue.Value;
									if (!name.Equals (propVal)) {
										delayed.Add (ErrorHelper.CreateWarning (1062, $"Invalid RegisterAttribute found on class '{t.FullName}'. Conflicting values specified for 'Name', using value of named parameter: '{propVal}' instead of constructor parameter: '{name}'."));
									}
									name = propVal;
								}
								break;
							case "SkipRegistration":
								delayed.Add (ErrorHelper.CreateWarning (1063, $"Invalid RegisterAttribute found on class '{t.FullName}'. SkipRegistration is not supported, and will be ignored."));
								break;
							default:
								delayed.Add (ErrorHelper.CreateWarning (1064, $"Invalid RegisterAttribute found on class '{t.FullName}'. Named parameter {propName} is not supported and will be ignored."));
								break;
							}
						}
					}
					if (!string.IsNullOrWhiteSpace (name)) {
						ErrorHelper.Show (delayed);
						return name;
					}
					// Fall back to the default strategy.
					delayed.Add (ErrorHelper.CreateWarning (1065, $"Invalid RegisterAttribute found on class '{t.FullName}'. No name argument for was provided to the constructor via either position or named parameter. Will use the default class naming convention instead."));
					ErrorHelper.Show (delayed);
				}
			}
			return t.FullName.Replace ('.', '_').Replace ("+", "_");
		}

		// TODO complete mapping (only with corresponding tests)
		// TODO override with attribute ? e.g. [Obj.Name ("XAMType")]
		public static string GetTypeName (Type t)
		{
			if (t.IsByRef) {
				var et = t.GetElementType ();
				if (Type.GetTypeCode (et) == TypeCode.Decimal) // This is boxed into NSDecimalNumber
					return GetTypeName (et) + "_Nonnull * _Nullable";

				return GetTypeName (et) + (et.IsValueType ? " " : " _Nonnull ") + "* _Nullable";
			}

			if (t.IsEnum)
				return GetObjCName (t);

			if (t.IsArray)
				return GetArrayTypeName (t.GetElementType ());

			switch (Type.GetTypeCode (t)) {
			case TypeCode.Object:
				switch (t.Namespace) {
				case "System":
					switch (t.Name) {
					case "Object":
					case "ValueType":
						return "NSObject";
					case "Void":
						return "void";
					default:
						return GetObjCName (t);
					}
				default:
					return GetObjCName (t);
				}
			case TypeCode.Boolean:
				return "bool";
			case TypeCode.Char:
				return "unsigned short";
			case TypeCode.Double:
				return "double";
			case TypeCode.Single:
				return "float";
			case TypeCode.Byte:
				return "unsigned char";
			case TypeCode.SByte:
				return "signed char";
			case TypeCode.Int16:
				return "short";
			case TypeCode.Int32:
				return "int";
			case TypeCode.Int64:
				return "long long";
			case TypeCode.UInt16:
				return "unsigned short";
			case TypeCode.UInt32:
				return "unsigned int";
			case TypeCode.UInt64:
				return "unsigned long long";
			case TypeCode.String:
				return "NSString *";
			case TypeCode.Decimal:
				return "NSDecimalNumber *";
			default:
				throw new NotImplementedException ($"Converting type {t.Name} to a native type name");
			}
		}

		public static string GetMonoName (Type t)
		{
			if (t.IsByRef)
				return GetMonoName (t.GetElementType ()) + "&";

			if (t.IsEnum)
				return t.FullName;

			if (t.IsArray)
				return $"{GetMonoName (t.GetElementType ())}[]";

			switch (Type.GetTypeCode (t)) {
			case TypeCode.Object:
				switch (t.Namespace) {
				case "System":
					switch (t.Name) {
					case "Void":
						return "void";
					default:
						return t.IsInterface ? t.FullName : "object";
					}
				default:
					return t.FullName;
				}
			case TypeCode.Boolean:
				return "bool";
			case TypeCode.Char:
				return "char";
			case TypeCode.Double:
				return "double";
			case TypeCode.Single:
				return "single";
			case TypeCode.Byte:
				return "byte";
			case TypeCode.SByte:
				return "sbyte";
			case TypeCode.Int16:
				return "int16";
			case TypeCode.Int32:
				return "int";
			case TypeCode.Int64:
				return "long";
			case TypeCode.UInt16:
				return "uint16";
			case TypeCode.UInt32:
				return "uint";
			case TypeCode.UInt64:
				return "ulong";
			case TypeCode.String:
				return "string";
			case TypeCode.Decimal:
				return "System.Decimal";
			default:
				throw new NotImplementedException ($"Converting type {t.Name} to a mono type name");
			}
		}

		public static string GetArrayTypeName (Type t)
		{
			switch (Type.GetTypeCode (t)) {
			case TypeCode.Boolean:
			case TypeCode.Char:
			case TypeCode.Double:
			case TypeCode.Single:
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
			case TypeCode.UInt64:
				return "NSArray <NSNumber *> *";
			case TypeCode.Byte:
				return "NSData *";
			case TypeCode.String:
				return "NSArray <NSString *> *";
			case TypeCode.Object:
				if (t.IsInterface)
					return $"NSArray<id<{GetObjCName (t)}>> *";

				return $"NSArray<{GetObjCName (t)} *> *";
			case TypeCode.Decimal:
				return "NSArray <NSDecimalNumber *> *";
			default:
				throw new NotImplementedException ($"Converting type {t.Name} to a native type name");
			}
		}

		public static string GetParameterTypeName (Type t)
		{
			if (t.IsArray)
				return t.GetElementType ().Name + "Array";
			if (t.IsByRef)
				return t.GetElementType ().Name + "Ref";
			if (!ObjCTypeToMethodName.TryGetValue (GetTypeName (t), out string name))
				name = t.Name;
			return name;
		}

		public static string GetExtendedParameterName (ParameterInfo p, ParameterInfo [] parameters)
		{
			string pName = p.Name;
			string ptname = GetTypeName (p.ParameterType);
			if (p.Name.Length < 3) {
				if (!ObjCTypeToArgument.TryGetValue (ptname, out pName))
					pName = "anObject";

				if (parameters.Count (p2 => GetTypeName (p2.ParameterType) == ptname && p2.Name.Length < 3) > 1 ||
					pName == "anObject" && parameters.Count (p2 => !ObjCTypeToArgument.ContainsKey (GetTypeName (p2.ParameterType))) > 1)
					pName += p.Name.PascalCase ();
			}

			return pName;
		}

		public static string GetObjCParamTypeName (ParameterInfo param, List<ProcessedType> allTypes)
		{
			Type pt = param.ParameterType;
			string ptname = GetTypeName (pt);
			if (pt.IsInterface)
				ptname = $"id<{ptname}>";
			if (allTypes.HasClass (pt))
				ptname += " *";
			return ptname;
		}
	}
}
