using System;
using System.Collections.Generic;
using System.Reflection;

namespace PageOfBob.Backup
{
    public interface IRootFactory
    {
        object Instantiate(dynamic config);
    }

    public interface IFactory
    {
        object Instantiate(IRootFactory parent, dynamic config);
    }

    public class RootFactory : IRootFactory
    {
        class FactoryInstanceInfo
        {
            public FactoryInstanceInfo(PluginAttribute details)
            {
                Details = details;
            }

            public PluginAttribute Details { get; }

            IFactory factoryInstance;
            public IFactory FactoryInstance
            {
                get
                {
                    if (factoryInstance != null)
                        return factoryInstance;

                    factoryInstance = Activator.CreateInstance(Details.FactoryType) as IFactory;
                    if (factoryInstance == null)
                        throw new InvalidOperationException($"Could not instantiate {Details.FactoryType.AssemblyQualifiedName} as IFactory");
                    return factoryInstance;
                }
            }
        }

        private readonly Dictionary<string, FactoryInstanceInfo> instances;

        RootFactory(Dictionary<string, FactoryInstanceInfo> instances)
        {
            this.instances = instances;
        }

        public static RootFactory CreateInstance(string[] plugins)
        {
            var dictionary = new Dictionary<string, FactoryInstanceInfo>();
            foreach (string plugin in plugins)
            {
                Type type = Type.GetType(plugin);
                var pluginAttribute = type.GetTypeInfo().GetCustomAttribute<PluginAttribute>();
                if (pluginAttribute == null)
                    throw new InvalidOperationException($"Missing `Plugin` Attribute: {type.AssemblyQualifiedName}");

                var info = new FactoryInstanceInfo(pluginAttribute);
                dictionary.Add(pluginAttribute.Name, info);
            }

            return new RootFactory(dictionary);
        }

        public object Instantiate(dynamic config)
        {
            string name = (string)config.type;
            if (!instances.TryGetValue(name, out FactoryInstanceInfo info))
                throw new InvalidOperationException($"Could not find plugin {name ?? "[NULL]"}");

            return info.FactoryInstance.Instantiate(this, config.config);
        }
    }
}
