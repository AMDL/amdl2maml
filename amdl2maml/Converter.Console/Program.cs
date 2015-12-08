﻿using Ditto.CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter.Console
{
    class Program
    {
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        static void Main(string[] args)
        {
            Parameters parameters;
            var results = CommandLine<Parameters>.TryParse(args, out parameters);
            if (results.Any() || parameters.Help)
            {
                foreach (var result in results)
                    System.Console.Error.WriteLine(result.ErrorMessage);
                if (results.Any())
                    System.Console.Out.WriteLine();
                CommandLine<Parameters>.WriteUsage(System.Console.Out);
                return;
            }
            Convert(parameters);
        }

        private static void Convert(Parameters parameters)
        {
            var srcPath = GetPath(parameters.SourcePath, true);
            var destPath = GetPath(parameters.DestinationPath, true);
            var layoutPath = GetPath(parameters.ContentLayoutPath, false);
            var timeFormat = parameters.TimeFormat;
            var verbosity = parameters.Verbosity;

            var task = ConvertAsync(srcPath, destPath, layoutPath, parameters, CancellationToken.None);
            task.GetAwaiter().GetResult();
        }

        private static string GetPath(string rawPath, bool isDirectory)
        {
            var destDir = isDirectory
                ? new DirectoryInfo(rawPath)
                : (FileSystemInfo)new FileInfo(rawPath);
            return destDir.FullName;
        }

        private static async Task<CancellationToken> ConvertAsync(string srcPath, string destPath, string layoutPath, Parameters parameters, CancellationToken cancellationToken)
        {
            using (var stream = System.Console.OpenStandardOutput())
            using (var writer = new StreamWriter(stream))
            {
                var startTime = DateTime.Now;

                await WritePrologueAsync(startTime, parameters, cancellationToken, writer);

                var title2id = await RunAsync((t, _) =>
                    LayoutIndexer.IndexAsync(layoutPath, t),
                    parameters, cancellationToken, writer, "READING ");

                var topics = await RunAsync((t, _) =>
                    FolderIndexer.IndexAsync(srcPath, t),
                    parameters, cancellationToken, writer, "INDEXING");

                topics = await RunAsync((t, p) =>
                    TopicParser.ParseAsync(topics, srcPath, t, p),
                    parameters, cancellationToken, writer, "PARSING ", "Parsing  {0}");

                topics = await RunAsync((t, _) =>
                    UpdateAsync(srcPath, title2id, topics),
                    parameters, cancellationToken, writer, "UPDATING");

                var name2topic = await RunAsync((t, _) =>
                    MapAsync(topics),
                    parameters, cancellationToken, writer, "MAPPING ");

                await RunAsync((t, p) =>
                    ConvertAsync(srcPath, destPath, topics, name2topic, t, p),
                    parameters, cancellationToken, writer, "WRITING ", "Writing  {0}");

                var endTime = DateTime.Now;

                await WriteEpilogueAsync(startTime, endTime, parameters, cancellationToken, writer);
            }
            return cancellationToken;
        }

        private static Task<IEnumerable<TopicData>> UpdateAsync(string srcPath, IDictionary<string, Guid> title2id, IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => TopicUpdater.Update(topics, srcPath, title2id));
        }

        private static Task<Dictionary<string, TopicData>> MapAsync(IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => topics.ToDictionary(topic => topic.Name, topic => topic));
        }

        private static async Task<object> ConvertAsync(string srcPath, string destPath, IEnumerable<TopicData> topics, Dictionary<string, TopicData> name2topic,
            CancellationToken cancellationToken, IProgress<string> progress)
        {
            await TopicConverter.ConvertAsync(topics, srcPath, destPath, name2topic, cancellationToken, progress);
            return null;
        }

        private static async Task<TResult> RunAsync<TResult>(Func<CancellationToken, IProgress<string>, Task<TResult>> taskFactory,
            Parameters parameters, CancellationToken cancellationToken, TextWriter writer, string title, string format = null)
        {
            var stepStartTime = DateTime.Now;

            await WriteStepPrologueAsync(title, stepStartTime, parameters, cancellationToken, writer);

            Progress<string> progress;
            EventHandler<string> handler;
            StartProgress(format, parameters, cancellationToken, writer, out progress, out handler);

            var result = await taskFactory(cancellationToken, progress);

            var stepEndTime = DateTime.Now;

            StopProgress(progress, handler);

            await WriteStepEpilogueAsync(title, stepStartTime, stepEndTime, parameters, cancellationToken, writer);

            return result;
        }

        #region Progress

        private static void StartProgress(string format, Parameters parameters, CancellationToken cancellationToken, TextWriter writer, out Progress<string> progress, out EventHandler<string> handler)
        {
            if (format == null || parameters.Verbosity < Verbosity.Detailed)
            {
                handler = null;
                progress = null;
                return;
            }
            handler = new EventHandler<string>((_, v) => WriteProgress(format, v, parameters, cancellationToken, writer));
            progress = new Progress<string>();
            progress.ProgressChanged += handler;
        }

        private static void StopProgress(Progress<string> progress, EventHandler<string> handler)
        {
            if (progress != null)
                progress.ProgressChanged -= handler;
        }

        #endregion Progress

        private static void WriteProgress(string format, string value, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            _semaphore.Wait(cancellationToken);
            Write(DateTime.Now, parameters, writer);
            writer.WriteLine(format, value);
            _semaphore.Release();
        }

        private const string PrologueFormat = "STARTED";

        private static async Task WritePrologueAsync(DateTime startTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await writer.WriteLineAsync();
            await WriteAsync(startTime, parameters, writer);
            await writer.WriteLineAsync(PrologueFormat);
            _semaphore.Release();
        }

        private const string EpilogueFormat = "TOTAL    {0}";

        private static async Task WriteEpilogueAsync(DateTime startTime, DateTime endTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await writer.WriteLineAsync();
            await WriteAsync(endTime, parameters, writer);
            await WriteAsync(EpilogueFormat, parameters, writer, endTime - startTime);
            await writer.WriteLineAsync();
            _semaphore.Release();
        }

        private const string StepPrologueFormat = "STARTED  {0}";

        private static async Task WriteStepPrologueAsync(string stepTitle, DateTime stepStartTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(stepStartTime, parameters, writer);
            await writer.WriteLineAsync(string.Format(StepPrologueFormat, stepTitle));
            _semaphore.Release();
        }

        private const string StepEpilogueFormat = "FINISHED {1} IN {0}";

        private static async Task WriteStepEpilogueAsync(string stepTitle, DateTime stepStartTime, DateTime stepEndTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(stepEndTime, parameters, writer);
            await WriteLineAsync(StepEpilogueFormat, parameters, writer, stepEndTime - stepStartTime, stepTitle);
            await writer.WriteLineAsync();
            _semaphore.Release();
        }

        private static void Write(DateTime time, Parameters parameters, TextWriter writer)
        {
            Write("{0} ", GetTimeFormat(parameters), writer.Write, time);
        }

        private static Task WriteAsync(DateTime time, Parameters parameters, TextWriter writer)
        {
            return WriteAsync("{0} ", GetTimeFormat(parameters), writer.WriteAsync, time);
        }

        private static Task WriteAsync(string format, Parameters parameters, TextWriter writer, params object[] args)
        {
            return WriteAsync(format, GetDurationFormat(parameters), writer.WriteAsync, args);
        }

        private static Task WriteLineAsync(string format, Parameters parameters, TextWriter writer, params object[] args)
        {
            return WriteAsync(format, GetDurationFormat(parameters), writer.WriteLineAsync, args);
        }

        private static string GetTimeFormat(Parameters parameters)
        {
            return Reformat(parameters.TimeFormat);
        }

        private static string GetDurationFormat(Parameters parameters)
        {
            var init = parameters.DurationFormat.Trim();
            var split = parameters.DurationFormat.Split(':', '.', ',', '-', ' ');
            int index = 0;
            string durationFormat = null;
            foreach (var s in split)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    durationFormat += Reformat(s);
                    index += s.Length;
                }
                if (index < init.Length - 1)
                    durationFormat += init[index++];
            }
            return durationFormat;
        }

        private static void Write(string format, string reFormat, Action<string> write, params object[] args)
        {
            format = format.Replace("{0}", reFormat);
            var value = string.Format(format, args);
            write(value);
        }

        private static Task WriteAsync(string format, string reFormat, Func<string, Task> write, params object[] args)
        {
            format = format.Replace("{0}", reFormat);
            var value = string.Format(format, args);
            return write(value);
        }

        private static string Reformat(string s)
        {
            return string.Format("{{0:{0}}}", s);
        }
    }
}