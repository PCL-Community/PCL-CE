using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Test;

[TestClass]
public class TaskTest
{
    [TestMethod]
    public void PipelineTaskTester()
    {
        PipelineTask<int> pipe = new("Test", [A, B, C]);
        Console.WriteLine(pipe.Run(1, 2, 3));
        return;
        int A(TaskBase<object> i, int x, int y, int z)
        {
            i.Progress = 1;
            Console.WriteLine($"In a: {x} {y} {z}");
            return x + y + z;
        }
        int B(TaskBase<object> i, int k) 
        {
            i.Progress = 1;
            Console.WriteLine("In b: " + k);
            return k;
        }
        int C(TaskBase<object> i, int k) 
        {
            i.Progress = 1;
            Console.WriteLine("In c: " + k);
            return k;
        }
    }
    
    [TestMethod]
    public async Task AsyncPipelineTaskTester()
    {
        PipelineTask<int> pipe = new("Test Async", [A, B, C]);
        Console.WriteLine(await pipe.RunAsync(1, 2, 3));
        return;
        int A(TaskBase<object> i, int x, int y, int z)
        {
            i.Progress = 1;
            Console.WriteLine($"In a: {x} {y} {z}");
            return x + y + z;
        }
        int B(TaskBase<object> i, int k) 
        {
            i.Progress = 1;
            Console.WriteLine("In b: " + k);
            return k;
        }
        int C(TaskBase<object> i, int k) 
        {
            i.Progress = 1;
            Console.WriteLine("In c: " + k);
            return k;
        }
    }
    
    [TestMethod]
    public async Task AsyncTaskTester()
    {
        TaskBase<int> task = new("Test Async", A);
        Console.WriteLine(await task.RunAsync(1, 2, 3));
        return;
        int A(TaskBase<int> i, int x, int y, int z)
        {
            i.Progress = 1;
            Console.WriteLine($"In a: {x} {y} {z}");
            return x + y + z;
        }
    }
}
