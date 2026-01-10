using Xcaciv.Cupcake.Core;
using Xcaciv.Command.Interface;
using Xcaciv.Command;
using System.Threading.Channels;

namespace Xcaciv.Cupcake.Core.Tests
{
    public class FakeIoContext : IIoContext, ICommandContext<IIoContext>, IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; } = "Fake";
        public bool IsInteractive { get; set; } = true;
        public bool HasPipedInput { get; set; } = false;
        public int? PipelineStage { get; private set; }
        public int? PipelineTotalStages { get; private set; }

        public Guid? Parent { get; set; }

        private string[] _parameters = Array.Empty<string>();
        public string[] Parameters => _parameters;
        public Task SetParameters(string[] parameters)
        {
            _parameters = parameters ?? Array.Empty<string>();
            return Task.CompletedTask;
        }

        public Task<IIoContext> GetChild(string[]? childParameters = null)
        {
            var child = new FakeIoContext { Parent = this.Id };
            if (childParameters != null) child.SetParameters(childParameters);
            return Task.FromResult<IIoContext>(child);
        }

        public Task HandleOutputChunk(IResult<string> chunk) => Task.CompletedTask;
        public Task OutputChunk(IResult<string> chunk) => Task.CompletedTask;
        public Task<string> PromptForCommand(string prompt) => Task.FromResult("END");

        public void SetOutputPipe(ChannelWriter<IResult<string>> outputPipe) { }
        public Task SetOutputPipe(Stream outputPipe) => Task.CompletedTask;

        public void SetInputPipe(ChannelReader<IResult<string>> inputPipe) { }
        public Task SetInputPipe(Stream inputPipe) => Task.CompletedTask;

        public void SetOutputEncoder(IOutputEncoder encoder) { }
        
        public void SetPipelineStage(int stage, int totalStages)
        {
            PipelineStage = stage;
            PipelineTotalStages = totalStages;
        }

        public async IAsyncEnumerable<IResult<string>> ReadInputPipeChunks()
        {
            yield break;
        }

        public Task SetStatusMessage(string message) => Task.CompletedTask;
        public Task AddTraceMessage(string message) => Task.CompletedTask;

        public Task<int> SetProgress(int total, int processed) => Task.FromResult(processed);

        public Task Complete(string? finalMessage = null) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public class FakeController : ICommandController
    {
        public void AddPackageDirectory(string path) { }
        public void RegisterBuiltInCommands() { }
        public void LoadCommands(string? configPath = null) { }
        public Task Run(string commandLine, IIoContext context, IEnvironmentContext env) => Task.CompletedTask;
        public Task Run(string commandLine, IIoContext context, IEnvironmentContext env, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetIoContext(IIoContext context) => Task.CompletedTask;
        public Task<IEnumerable<string>> GetCommandNames() => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public void GetHelp(string commandName, IIoContext context, IEnvironmentContext env) { }
        public Task GetHelpAsync(string commandName, IIoContext context, IEnvironmentContext env, CancellationToken cancellationToken) => Task.CompletedTask;
        public void AddCommand(ICommandDescription description) { }
        public void AddCommand(string name, Type commandType, bool enabled) { }
        public void AddCommand(string name, ICommandDelegate commandDelegate, bool enabled) { }
    }

    public class FakeEnvironment : IEnvironmentContext, ICommandContext<IEnvironmentContext>, IAsyncDisposable
    {
        private readonly Dictionary<string, string> _values = new();
        public bool HasChanged { get; private set; }
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; } = "Env";
        public Guid? Parent { get; set; }

        public string GetValue(string key) => _values.TryGetValue(key, out var v) ? v : string.Empty;
        public string GetValue(string key, string defaultValue, bool require)
        {
            if (_values.TryGetValue(key, out var v)) return v;
            if (require) throw new KeyNotFoundException(key);
            return defaultValue;
        }
        public Dictionary<string, string> GetEnvinronment() => new(_values);
        public Dictionary<string, string> GetEnvironment() => new(_values);
        public void UpdateEnvironment(Dictionary<string, string> values)
        {
            foreach (var kvp in values)
            {
                _values[kvp.Key] = kvp.Value;
            }
            HasChanged = true;
        }
        public void SetValue(string key, string value)
        {
            _values[key] = value;
            HasChanged = true;
        }

        public Task<IEnvironmentContext> GetChild(string[]? childParameters = null)
        {
            var child = new FakeEnvironment { Parent = this.Id };
            return Task.FromResult<IEnvironmentContext>(child);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public class LoopTests
    {
        [Fact]
        public void Run_ExitsOnEnd()
        {
            var loop = new Loop();
            var io = new FakeIoContext();
            var ctrl = new FakeController();
            var env = new FakeEnvironment();

            loop.Run(io, ctrl, env);

            Assert.NotNull(loop.Controller);
            Assert.NotNull(loop.Environment);
        }

        [Fact]
        public async Task RunAsync_ExitsOnEnd()
        {
            var loop = new Loop();
            var io = new FakeIoContext();
            var ctrl = new FakeController();
            var env = new FakeEnvironment();

            await loop.RunAsync(io, ctrl, env);

            Assert.NotNull(loop.Controller);
            Assert.NotNull(loop.Environment);
        }

        [Fact]
        public void Loop_Defaults_Initialized()
        {
            var loop = new Loop();
            Assert.True(loop.EnableInstallCommand);
            Assert.False(string.IsNullOrEmpty(loop.Prompt));
            Assert.NotEmpty(loop.ExitCommands);
            Assert.False(string.IsNullOrEmpty(loop.PackageDirectory));
        }
    }
}
