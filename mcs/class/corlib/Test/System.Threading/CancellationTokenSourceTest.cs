//
// CancellationTokenSourceTest.cs
//
// Authors:
//       Marek Safar (marek.safar@gmail.com)
//       Jeremie Laval (jeremie.laval@gmail.com)
//
// Copyright 2011 Xamarin, Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

#if NET_4_0 || MOBILE

using System;
using System.Threading;
using NUnit.Framework;
using System.Threading.Tasks;

namespace MonoTests.System.Threading
{
	[TestFixture]
	public class CancellationTokenSourceTest
	{
		[Test]
		public void Token ()
		{
			CancellationTokenSource cts = new CancellationTokenSource ();
			Assert.IsTrue (cts.Token.CanBeCanceled, "#1");
			Assert.IsFalse (cts.Token.IsCancellationRequested, "#2");
			Assert.IsNotNull (cts.Token.WaitHandle, "#3");
		}

		[Test]
		public void Cancel_NoRegistration ()
		{
			CancellationTokenSource cts = new CancellationTokenSource ();
			cts.Cancel ();
		}

		[Test]
		public void Cancel ()
		{
			var cts = new CancellationTokenSource ();

			int called = 0;
			cts.Token.Register (l => { Assert.AreEqual ("v", l); ++called; }, "v");
			cts.Cancel ();
			Assert.AreEqual (1, called, "#1");

			called = 0;
			cts.Token.Register (() => { called += 12; });
			cts.Cancel ();
			Assert.AreEqual (12, called, "#2");
		}

		[Test]
		public void Cancel_SingleException ()
		{
			var cts = new CancellationTokenSource ();

			cts.Token.Register (() => { throw new ApplicationException (); });
			try {
				cts.Cancel ();
				Assert.Fail ("#1");
			} catch (AggregateException e) {
				Assert.AreEqual (1, e.InnerExceptions.Count, "#2");
			}

			cts.Cancel ();
		}

		[Test]
		public void Cancel_MultipleExceptions ()
		{
			var cts = new CancellationTokenSource ();

			cts.Token.Register (() => { throw new ApplicationException ("1"); });
			cts.Token.Register (() => { throw new ApplicationException ("2"); });
			cts.Token.Register (() => { throw new ApplicationException ("3"); });

			try {
				cts.Cancel ();
				Assert.Fail ("#1");
			} catch (AggregateException e) {
				Assert.AreEqual (3, e.InnerExceptions.Count, "#2");
			}

			cts.Cancel ();

			try {
				cts.Token.Register (() => { throw new ApplicationException ("1"); });
				Assert.Fail ("#11");
			} catch (ApplicationException) {
			}

			cts.Cancel ();
		}

		[Test]
		public void Cancel_MultipleExceptionsFirstThrows ()
		{
			var cts = new CancellationTokenSource ();

			cts.Token.Register (() => { throw new ApplicationException ("1"); });
			cts.Token.Register (() => { throw new ApplicationException ("2"); });
			cts.Token.Register (() => { throw new ApplicationException ("3"); });

			try {
				cts.Cancel (true);
				Assert.Fail ("#1");
			} catch (ApplicationException) {
			}

			cts.Cancel ();
		}

		[Test]
		public void CreateLinkedTokenSource_InvalidArguments ()
		{
			var cts = new CancellationTokenSource ();
			var token = cts.Token;

			try {
				CancellationTokenSource.CreateLinkedTokenSource (null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException) {
			}

			try {
				CancellationTokenSource.CreateLinkedTokenSource (new CancellationToken[0]);
				Assert.Fail ("#2");
			} catch (ArgumentException) {
			}
		}

		[Test]
		public void CreateLinkedTokenSource ()
		{
			var cts = new CancellationTokenSource ();
			cts.Cancel ();

			var linked = CancellationTokenSource.CreateLinkedTokenSource (cts.Token);
			Assert.IsTrue (linked.IsCancellationRequested, "#1");

			linked = CancellationTokenSource.CreateLinkedTokenSource (new CancellationToken ());
			Assert.IsFalse (linked.IsCancellationRequested, "#2");
		}

		[Test]
		public void Dispose ()
		{
			var cts = new CancellationTokenSource ();
			var token = cts.Token;

			cts.Dispose ();
			cts.Dispose ();
			var b = cts.IsCancellationRequested;
			token.ThrowIfCancellationRequested ();

			try {
				cts.Cancel ();
				Assert.Fail ("#1");
			} catch (ObjectDisposedException) {
			}

			try {
				var t = cts.Token;
				Assert.Fail ("#2");
			} catch (ObjectDisposedException) {
			}

			try {
				token.Register (() => { });
				Assert.Fail ("#3");
			} catch (ObjectDisposedException) {
			}

			try {
				var wh = token.WaitHandle;
				Assert.Fail ("#4");
			} catch (ObjectDisposedException) {
			}

			try {
				CancellationTokenSource.CreateLinkedTokenSource (token);
				Assert.Fail ("#5");
			} catch (ObjectDisposedException) {
			}
		}

		[Test]
		public void RegisterThenDispose ()
		{
			var cts1 = new CancellationTokenSource ();
			var reg1 = cts1.Token.Register (() => { throw new ApplicationException (); });

			var cts2 = new CancellationTokenSource ();
			var reg2 = cts2.Token.Register (() => { throw new ApplicationException (); });

			Assert.AreNotEqual (cts1, cts2, "#1");
			Assert.AreNotSame (cts1, cts2, "#2");

			reg1.Dispose ();
			cts1.Cancel ();

			try {
				cts2.Cancel ();
				Assert.Fail ("#3");
			} catch (AggregateException) {
			}
		}

		[Test]
		public void RegisterWhileCancelling ()
		{
			var cts = new CancellationTokenSource ();
			var mre = new ManualResetEvent (false);
			var mre2 = new ManualResetEvent (false);
			int called = 0;

			cts.Token.Register (() => {
				Assert.IsTrue (cts.IsCancellationRequested, "#10");
				Assert.IsTrue (cts.Token.WaitHandle.WaitOne (0), "#11");
				mre2.Set ();
				mre.WaitOne (3000);
				called += 11;
			});

			var t = Task.Factory.StartNew (() => { cts.Cancel (); });

			Assert.IsTrue (mre2.WaitOne (1000), "#0");
			cts.Token.Register (() => { called++; });
			Assert.AreEqual (1, called, "#1");
			Assert.IsFalse (t.IsCompleted, "#2");

			mre.Set ();
			Assert.IsTrue (t.Wait (1000), "#3");
			Assert.AreEqual (12, called, "#4");
		}

		[Test]
		public void ReEntrantRegistrationTest ()
		{
			bool unregister = false;
			bool register = false;
			var source = new CancellationTokenSource ();
			var token = source.Token;

			var reg = new CancellationTokenRegistration ();
			Console.WriteLine ("Test1");
			token.Register (() => reg.Dispose ());
			reg = token.Register (() => unregister = true);
			token.Register (() => { Console.WriteLine ("Gnyah"); token.Register (() => register = true); });
			source.Cancel ();

			Assert.IsFalse (unregister);
			Assert.IsTrue (register);
		}

		[Test]
		public void DisposeAfterRegistrationTest ()
		{
			var source = new CancellationTokenSource ();
			bool ran = false;
			var req = source.Token.Register (() => ran = true);
			source.Dispose ();
			req.Dispose ();
			Assert.IsFalse (ran);
		}
	}
}

#endif

