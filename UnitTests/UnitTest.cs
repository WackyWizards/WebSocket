global using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WebSocket.Tests;

[TestClass]
public class TestInit
{
	[AssemblyInitialize]
	public static void ClassInitialize( TestContext context )
	{
		Sandbox.Application.InitUnitTest();
	}
	
	[AssemblyCleanup]
	public static void AssemblyCleanup()
	{
		Sandbox.Application.ShutdownUnitTest();
	}
}
