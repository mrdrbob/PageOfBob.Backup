using System;

namespace PageOfBob.Backup
{

    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAttribute : Attribute
    {
        public PluginAttribute(string name, Type factoryType)
        {
            Name = name;
            FactoryType = factoryType;
        }

        public string Name { get; }
        public Type FactoryType { get; }
    }
}
