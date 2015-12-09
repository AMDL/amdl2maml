using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter
{
    class Step<T>
    {
        private readonly string title;
        private readonly Func<T, Task<T>> taskFactory;

        public Step(string title, Func<T, Task<T>> taskFactory)
        {
            this.title = title;
            this.taskFactory = taskFactory;
        }

        private async Task<T> ExecuteAsync(T data, IProgress<Indicator> progress, int index, int count)
        {
            Indicator.Report(progress, count, index, title);
            return await taskFactory(data);
        }

        public static async Task<T> ExecuteAllAsync(T data, IEnumerable<Step<T>> steps, IProgress<Indicator> progress)
        {
            var stepsArray = steps.ToArray();
            var count = stepsArray.Length;
            for (int index = 0; index < count; index++)
                data = await stepsArray[index].ExecuteAsync(data, progress, index, count);
            return data;
        }
    }
}
