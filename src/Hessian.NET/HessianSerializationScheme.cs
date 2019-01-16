﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Hessian.Net
{
    internal class HessianSerializationScheme
    {
        public Type ObjectType
        {
            get;
            private set;
        }

        public ISerializationElement Element
        {
            get;
        }

        private HessianSerializationScheme(Type objectType, ISerializationElement element)
        {
            ObjectType = objectType;
            Element = element;
        }

        public static HessianSerializationScheme CreateFromType(Type type, IObjectSerializerFactory factory)
        {
            var catalog = new Dictionary<Type, ISerializationElement>();
            var element = CreateSerializationElement(type, catalog, factory);

            return new HessianSerializationScheme(type, element);
        }

        public void Serialize(HessianOutputWriter writer, object graph, HessianSerializationContext context)
        {
            Element.Serialize(writer, graph, context);
        }

        public object Deserialize(HessianInputReader reader, HessianSerializationContext context)
        {
            return Element.Deserialize(reader, context);
        }

        private static ISerializationElement CreateSerializationElement(Type type, IDictionary<Type, ISerializationElement> catalog, IObjectSerializerFactory factory)
        {
            var info = type.GetTypeInfo();

            if (IsSimpleType(info))
            {
                var serializer = factory.GetSerializer(type);
                return new ValueElement(type, serializer);
            }

            if (IsListType(info))
            {
                return new ListElement(type, catalog, factory);
            }
            return BuildSerializationObject(type, catalog, factory);
        }
         
        private static ISerializationElement BuildSerializationObject(Type type, IDictionary<Type, ISerializationElement> catalog, IObjectSerializerFactory factory)
        {
            ISerializationElement existing;

            if (catalog.TryGetValue(type, out existing))
            {
                return existing;
            }

            var info = type.GetTypeInfo();
            var contract = info.GetCustomAttribute<DataContractAttribute>();

            if (null == contract)
            {
                throw new Exception();
            }

            var properties = new List<PropertyElement>();
            var element = new ObjectElement(type, properties);

            catalog.Add(type, element);

            foreach (var property in info.DeclaredProperties)
            {
                var attribute = property.GetCustomAttribute<DataMemberAttribute>();

                if (null == attribute)
                {
                    continue;
                }

                if (!property.CanRead || !property.CanWrite)
                {
                    continue;
                }

                var prop = new PropertyElement(property, CreateSerializationElement(property.PropertyType, catalog, factory));

                properties.Add(prop);
            }

            properties.Sort(new ObjectPropertyComparer());

            return element;
        }

        private static bool IsSimpleType(TypeInfo typeInfo)
        {
            if (typeInfo.IsValueType || typeInfo.IsEnum || typeInfo.IsPrimitive)
            {
                return true;
            }

            if (typeof (String) == typeInfo.AsType())
            {
                return true;
            }

            return false;
        }

        public static bool IsListType(TypeInfo typeInfo)
        {
            return typeof(IEnumerable).IsAssignableFrom(typeInfo);          
        }
    }
}