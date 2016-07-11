using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TUtils.Common.Extensions;
using TUtils.Common.Reflection;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core.Serializer
{
	public class MessageSerializer : IMessageSerializer
	{
		private readonly NetSerializer.Serializer _serializer;

		/// <summary>
		/// Initializes NetSerializer.
		/// Iterates through all assemblies down from "rootAssembly" and looks for types, which have attribute
		/// [Serializable] (or one of it's base classes) and contains "Message" in type-name.
		/// </summary>
		/// <param name="rootAssemblies">
		/// Default (in case of null): Assembly.GetEntryAssembly()
		/// The constructor looks in rootAssembly and all referenced assemblies for types with attribute [Serializable].
		/// </param>
		/// <param name="blacklistFilter"></param>
		/// <param name="additionalTypes"></param>
		public MessageSerializer(
			List<Assembly> rootAssemblies,
			Func<Type, bool> blacklistFilter,
			List<Type> additionalTypes)
		{
			var listOfAllSerializableTypes = new List<Type>();

			AssemblyLib.DoForAllTypesOfAllAssemblies(rootAssemblies, typeof(SerializableAttribute), type =>
			{
				if (!type.FullName.StartsWith("System")
					&& !type.Name.Contains("<")
					&& !type.FullName.StartsWith("Microsoft")
					&& !type.FullName.StartsWith("Castle")
					&& !blacklistFilter(type)
					&& !typeof(ISerializable).IsAssignableFrom(type))
				{
					// ReSharper disable once AccessToModifiedClosure
					listOfAllSerializableTypes.Add(type);
				}	
			});

			if (additionalTypes != null)
			{
				listOfAllSerializableTypes.AddRange(additionalTypes);
				listOfAllSerializableTypes = listOfAllSerializableTypes.Distinct().ToList();
			}

			_serializer = new NetSerializer.Serializer(listOfAllSerializableTypes);
		}

		object IMessageSerializer.Deserialize(MessageContent messageContent)
		{
			using (var memStream = new MemoryStream(messageContent.GetData()))
			{
				return _serializer.Deserialize(memStream);
			}
		}

		MessageContent IMessageSerializer.Serialize(object message)
		{
			using (var memStream = new MemoryStream())
			{
				_serializer.Serialize(memStream, message);
				return new ByteMessageContent(memStream.ToArray());
			}
		}
	}
}