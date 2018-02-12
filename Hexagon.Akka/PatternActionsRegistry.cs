using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Hexagon.AkkaImpl
{
    public enum EActionType { Code, PowershellScript };

    public class PatternActionsRegistry<M, P>
        where P : IMessagePattern<M>
        where M : IMessage
    {
        public class MessageRegistryEntry
        {
            public P Pattern;
            public Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>> Action;
            public Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>, Task> AsyncAction;
            public string Script;
            public EActionType CodeType;
            public string Key;
        }

        List<MessageRegistryEntry> Registry;

        string _assemblyName = null;
        public string AssemblyName
        {
            get => _assemblyName;
            private set
            {
                if (_assemblyName == null)
                    _assemblyName = value;
                else if (_assemblyName != value)
                    throw new Exception($"Pattern actions registration function must be defined in 1 assembly only ({_assemblyName})");
            }
        }

        public PatternActionsRegistry()
        {
            Registry = new List<MessageRegistryEntry>();
        }

        public void AddAction(P pattern, Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>> action, string key)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                Action = action,
                CodeType = EActionType.Code,
                Key = key
            });
            AssemblyName = System.Reflection.Assembly.GetCallingAssembly().GetName().FullName;
        }

        public void AddAsyncAction(P pattern, Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>, Task> action, string key)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                AsyncAction = action,
                CodeType = EActionType.Code,
                Key = key
            });
            AssemblyName = Assembly.GetCallingAssembly().GetName().FullName;
        }

        public void AddPowershellScript(P pattern, System.Management.Automation.ScriptBlock script, string key)
        {
            AddPowershellScript(pattern, script.ToString(), key);
        }

        void AddPowershellScript(P pattern, string script, string key)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                Script = script,
                CodeType = EActionType.PowershellScript,
                Key = key
            });
        }

        public void AddPowershellScriptBody(P pattern, string scriptBody, string key)
        {
            AddPowershellScript(
                pattern, 
                string.Format("param([string]$message, $sender, $self, $messageSystem) {0}", scriptBody), 
                key);
        }

        public ILookup<string, MessageRegistryEntry> LookupByKey()
            => Registry.ToLookup(entry => entry.Key);

        // Load actions from the assembly, looking for a method with attribute PatternActionsRegistration
        // Returns Code actions only
        public static PatternActionsRegistry<M, P> FromAssembly(string assemblyName)
        {
            var assembly = Assembly.Load(assemblyName);
            var registrationMethod = assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod).Where(method => method.GetCustomAttributes<PatternActionsRegistrationAttribute>(false).Count() > 0)).First();
            PatternActionsRegistry<M, P> registry = new PatternActionsRegistry<M, P>();
            registrationMethod.Invoke(null, new[] { registry });
            return new PatternActionsRegistry<M, P> { Registry = registry.Registry.Where(entry => entry.CodeType == EActionType.Code).ToList() };
        }
    }
}
