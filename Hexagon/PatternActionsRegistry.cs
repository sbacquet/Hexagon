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
            public Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger> Action;
            public Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger, Task> AsyncAction;
            public string Code;
            public EActionType CodeType;
            public string ProcessingUnitId;
        }

        readonly List<MessageRegistryEntry> Registry;

        readonly Dictionary<string, Lazy<IDisposable>> ProcessingUnitResources;

        public PatternActionsRegistry()
        {
            Registry = new List<MessageRegistryEntry>();
            ProcessingUnitResources = new Dictionary<string, Lazy<IDisposable>>();
        }

        public void AddRegistry(PatternActionsRegistry<M, P> registry)
        {
            if (registry != null)
            {
                Registry.AddRange(registry.Registry);
                registry.ProcessingUnitResources.ToList().ForEach(x => ProcessingUnitResources[x.Key] = x.Value);
            }
        }

        public void AddAction(P pattern, Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger> action, string processingUnitId)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                Action = action,
                CodeType = EActionType.Code,
                Code = System.Reflection.Assembly.GetCallingAssembly().GetName().FullName,
                ProcessingUnitId = processingUnitId
            });
        }

        public void AddAsyncAction(P pattern, Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger, Task> action, string processingUnitId)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                AsyncAction = action,
                CodeType = EActionType.Code,
                Code = System.Reflection.Assembly.GetCallingAssembly().GetName().FullName,
                ProcessingUnitId = processingUnitId
            });
        }

        public void AddPowershellScript(P pattern, System.Management.Automation.ScriptBlock script, string processingUnitId)
        {
            AddPowershellScript(pattern, script.ToString(), processingUnitId);
        }

        public void AddPowershellScript(P pattern, string script, string processingUnitId)
        {
            Registry.Add(new MessageRegistryEntry
            {
                Pattern = pattern,
                Code = script,
                CodeType = EActionType.PowershellScript,
                ProcessingUnitId = processingUnitId
            });
        }

        public void AddPowershellScriptBody(P pattern, string scriptBody, string processingUnitId)
        {
            AddPowershellScript(
                pattern, 
                string.Format("param($message, $sender, $self, $resource, $messageSystem) {0}", scriptBody), 
                processingUnitId);
        }

        public ILookup<string, MessageRegistryEntry> LookupByProcessingUnit()
            => Registry.ToLookup(entry => entry.ProcessingUnitId);

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
            {
                Registry.AddRange(registry.Registry.Where(entry => filter(entry)));
                registry.ProcessingUnitResources.ToList().ForEach(x => ProcessingUnitResources[x.Key] = x.Value);
            }
        }

        public void SetProcessingUnitResource(string processingUnitId, Lazy<IDisposable> resource)
            => ProcessingUnitResources[processingUnitId] = resource;

        public Lazy<IDisposable> GetProcessingUnitResource(string processingUnitId)
        {
            ProcessingUnitResources.TryGetValue(processingUnitId, out Lazy<IDisposable> resource);
            return resource;
        }
    }
}
