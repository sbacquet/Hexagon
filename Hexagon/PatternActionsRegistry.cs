using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Hexagon
{
    public enum EActionType { Code, PowershellScript };

    public class PatternActionsRegistry<M, P>
        where P : IMessagePattern<M>
        where M : IMessage
    {
        public class MessageRegistryEntry
        {
            public P Pattern;
            public Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>, ILogger> Action;
            public Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>, ILogger, Task> AsyncAction;
            public string Code;
            public EActionType CodeType;
            public string Key;
        }

        List<MessageRegistryEntry> Registry;

        public PatternActionsRegistry()
        {
            Registry = new List<MessageRegistryEntry>();
        }

        public void AddRegistry(PatternActionsRegistry<M, P> registry)
        {
            if (registry != null)
                Registry.AddRange(registry.Registry);
        }

        public void AddAction(P pattern, Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>, ILogger> action, string key)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                Action = action,
                CodeType = EActionType.Code,
                Code = System.Reflection.Assembly.GetCallingAssembly().GetName().FullName,
                Key = key
            });
        }

        public void AddAsyncAction(P pattern, Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>, ILogger, Task> action, string key)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                AsyncAction = action,
                CodeType = EActionType.Code,
                Code = System.Reflection.Assembly.GetCallingAssembly().GetName().FullName,
                Key = key
            });
        }

        public void AddPowershellScript(P pattern, System.Management.Automation.ScriptBlock script, string key)
        {
            AddPowershellScript(pattern, script.ToString(), key);
        }

        public void AddPowershellScript(P pattern, string script, string key)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                Code = script,
                CodeType = EActionType.PowershellScript,
                Key = key
            });
        }

        public void AddPowershellScriptBody(P pattern, string scriptBody, string key)
        {
            AddPowershellScript(
                pattern, 
                string.Format("param($message, $sender, $self, $messageSystem) {0}", scriptBody), 
                key);
        }

        public ILookup<string, MessageRegistryEntry> LookupByKey()
            => Registry.ToLookup(entry => entry.Key);

        // Load actions from the assembly, looking for a method with attribute PatternActionsRegistration
        // Returns Code actions only
        public void AddActionsFromAssembly(string assemblyName, Predicate<MessageRegistryEntry> filter = null)
        {
            var assembly = Assembly.Load(assemblyName);
            var registrationMethods = assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod).Where(method => method.GetCustomAttributes<PatternActionsRegistrationAttribute>(false).Count() > 0));
            PatternActionsRegistry<M, P> registry = new PatternActionsRegistry<M, P>();
            foreach (var method in registrationMethods)
                method.Invoke(null, new[] { registry });
            if (filter == null)
                AddRegistry(registry);
            else
                Registry.AddRange(registry.Registry.Where(entry => filter(entry)));
        }
    }
}
