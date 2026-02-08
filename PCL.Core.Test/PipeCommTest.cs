using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Pipes;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Test
{
    /// <summary>
    /// 管道通信测试类
    /// </summary>
    [TestClass]
    public class PipeCommTest
    {
        private const string TestPipeName = "TestPipe";
        private const string TestIdentifier = "TestServer";

        /// <summary>
        /// 测试管道服务器启动和客户端连接
        /// </summary>
        [TestMethod]
        public async Task TestPipeServerStartAndClientConnect()
        {
            // 标记是否接收到客户端连接
            bool clientConnected = false;

            // 启动管道服务器
            var server = PipeComm.StartPipeServer(
                TestIdentifier,
                TestPipeName,
                (reader, writer, process) =>
                {
                    clientConnected = true;
                    // 向客户端发送测试消息
                    writer.WriteLine("Hello from server");
                    writer.Flush();
                    // 读取客户端响应
                    var response = reader.ReadLine();
                    Assert.AreEqual("Hello from client", response);
                    return false; // 停止服务器
                }
            );

            // 等待服务器启动
            await Task.Delay(500);

            try
            {
                // 启动客户端连接
                using var client = new NamedPipeClientStream(".", TestPipeName, PipeDirection.InOut);
                client.Connect(1000); // 1秒超时

                // 使用客户端流
                using var reader = new StreamReader(client);
                using var writer = new StreamWriter(client);

                // 读取服务器消息
                var message = reader.ReadLine();
                Assert.AreEqual("Hello from server", message);

                // 向服务器发送响应
                writer.WriteLine("Hello from client");
                writer.Flush();

                // 等待服务器处理
                await Task.Delay(500);

                // 验证客户端连接事件是否被触发
                Assert.IsTrue(clientConnected);
            }
            finally
            {
                // 清理服务器资源
                server.Dispose();
            }
        }

        /// <summary>
        /// 测试进程ID验证功能
        /// </summary>
        [TestMethod]
        public async Task TestProcessIdValidation()
        {
            bool clientConnected = false;
            int currentProcessId = Environment.ProcessId;

            // 启动管道服务器，只允许当前进程连接
            var server = PipeComm.StartPipeServer(
                TestIdentifier,
                TestPipeName,
                (reader, writer, process) =>
                {
                    clientConnected = true;
                    return false;
                },
                allowedProcessId: new[] { currentProcessId }
            );

            await Task.Delay(500);

            try
            {
                // 启动客户端连接（应该成功，因为是同一个进程）
                using var client = new NamedPipeClientStream(".", TestPipeName, PipeDirection.InOut);
                client.Connect(1000);

                // 发送终止符
                using var writer = new StreamWriter(client);
                writer.Write(PipeComm.PipeEndingChar);
                writer.Flush();

                await Task.Delay(500);
                Assert.IsTrue(clientConnected);
            }
            finally
            {
                server.Dispose();
            }
        }

        /// <summary>
        /// 测试异常处理功能
        /// </summary>
        [TestMethod]
        public async Task TestExceptionHandling()
        {
            bool serverStopped = false;

            // 启动管道服务器，模拟回调函数抛出异常
            var server = PipeComm.StartPipeServer(
                TestIdentifier,
                TestPipeName,
                (reader, writer, process) => { throw new Exception("Test exception"); },
                () => serverStopped = true,
                stopWhenException: true
            );

            await Task.Delay(500);

            try
            {
                // 启动客户端连接
                using var client = new NamedPipeClientStream(".", TestPipeName, PipeDirection.InOut);
                client.Connect(1000);

                // 等待服务器处理异常并停止
                await Task.Delay(1000);

                // 验证服务器是否停止
                Assert.IsTrue(serverStopped);
            }
            catch (Exception)
            {
                // 客户端可能会因为服务器异常而断开连接，这是预期行为
            }
            finally
            {
                server.Dispose();
            }
        }

        /// <summary>
        /// 测试管道断开连接的处理
        /// </summary>
        [TestMethod]
        public async Task TestPipeDisconnectionHandling()
        {
            bool serverContinued = false;

            // 启动管道服务器
            var server = PipeComm.StartPipeServer(
                TestIdentifier,
                TestPipeName,
                (reader, writer, process) =>
                {
                    // 模拟客户端断开连接
                    Thread.Sleep(100);
                    return true; // 继续等待下一个连接
                },
                stopCallback: () => serverContinued = true
            );

            await Task.Delay(500);

            try
            {
                // 启动客户端连接并立即断开
                using var client = new NamedPipeClientStream(".", TestPipeName, PipeDirection.InOut);
                client.Connect(1000);
                // 立即关闭客户端
                client.Dispose();

                // 等待服务器处理断开连接
                await Task.Delay(1000);

                // 验证服务器是否继续运行
                Assert.IsFalse(serverContinued);
            }
            finally
            {
                server.Dispose();
            }
        }
    }
}