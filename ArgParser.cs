// Copyright (c) 2024 Benoit Pelletier
// SPDX-License-Identifier: BSL-1.0
// Distributed under the Boost Software License, Version 1.0. 
// (See accompanying file LICENSE or copy at https://www.boost.org/LICENSE_1_0.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TransmuDoc
{
	public class RequiredAttribute : Attribute {}

	public class ArgParser<T>
	{
		internal class ArgInfo
		{
			public string Name { get { return member?.Name.ToLower(); } }

			public bool IsWritable
			{
				get
				{
					switch (member.MemberType)
					{
						case MemberTypes.Field:
							return true;
						case MemberTypes.Property:
							PropertyInfo property = (PropertyInfo)member;
							return property.CanWrite;
						default:
							throw new NotImplementedException($"Member type {member.MemberType} is not implemented.");
					}
				}
			}

			public bool IsRequired
			{
				get
				{
					return member?.GetCustomAttribute<RequiredAttribute>() != null;
				}
			}

			public Type ArgType
			{
				get
				{
					switch (member.MemberType)
					{
						case MemberTypes.Field:
							FieldInfo field = (FieldInfo)member;
							return field.FieldType;
						case MemberTypes.Property:
							PropertyInfo property = (PropertyInfo)member;
							return property.PropertyType;
						default:
							throw new NotImplementedException($"Member type {member.MemberType} is not implemented.");
					}
				}
			}

			public ArgInfo(MemberInfo member)
			{
				this.member = member;
			}

			public void SetValue(T obj, object value)
			{
				switch (member.MemberType)
				{
					case MemberTypes.Field:
						FieldInfo field = (FieldInfo)member;
						field.SetValue(obj, value);
						break;
					case MemberTypes.Property:
						PropertyInfo prop = (PropertyInfo)member;
						prop.SetValue(obj, value);
						break;
					default:
						throw new NotImplementedException($"Member type {member.MemberType} is not implemented.");
				}
			}

			private MemberInfo member;
		}

		public bool Parse(string[] args, ref T result)
		{
			BuildCacheInfos();

			List<ArgInfo> remainingRequired = new List<ArgInfo>();
			remainingRequired.AddRange(requiredArgs);

			foreach (string arg in args)
			{
				if (!arg.StartsWith("-"))
					continue;

				int valueIndex = arg.IndexOf('=');
				string argName = (valueIndex >= 0) ? arg.Substring(1, valueIndex - 1) : arg.Substring(1);
				argName = argName.ToLower();

				ArgInfo argInfo = argInfos.FirstOrDefault((ArgInfo x) => { return x.Name == argName; });
				if (argInfo == null)
				{
					Console.WriteLine($"Warning: unknown argument '{argName}'.");
					continue;
				}

				if (!argInfo.IsWritable)
				{
					Console.WriteLine($"Error: argument '{argName}' is read-only.");
					continue;
				}

				if (valueIndex < 0)
				{
					if (argInfo.ArgType != typeof(bool))
					{
						Console.WriteLine($"Error: argument '{argName}' is not of type bool.");
						continue;
					}

					argInfo.SetValue(result, true);
				}
				else
				{
					string varValue = arg.Substring(valueIndex + 1);

					// TODO: manage other types too (from parsing results)
					if (argInfo.ArgType != typeof(string))
					{
						Console.WriteLine($"Error: argument '{argName}' is not of type string.");
						continue;
					}

					argInfo.SetValue(result, varValue);
				}

				remainingRequired.Remove(argInfo);
			}

			foreach(ArgInfo info in remainingRequired)
			{
				Console.WriteLine($"Error: Missing required argument '{info.Name}'.");
			}

			return remainingRequired.Count() <= 0;
		}

		private void BuildCacheInfos()
		{
			if (argInfos != null)
				return;

			argInfos = new List<ArgInfo>();

			argInfos.AddRange(typeof(T)
				.GetFields(BindingFlags.Instance | BindingFlags.Public)
				.Select((FieldInfo x) => { return new ArgInfo(x); }));
			argInfos.AddRange(typeof(T)
				.GetProperties(BindingFlags.Instance | BindingFlags.Public)
				.Select((PropertyInfo x) => { return new ArgInfo(x); }));

			requiredArgs = argInfos.Where((ArgInfo x) => { return x.IsRequired; }).ToArray();
		}

		private List<ArgInfo> argInfos = null;
		private ArgInfo[] requiredArgs = null;
	}
}
