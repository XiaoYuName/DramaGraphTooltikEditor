using System.Text.RegularExpressions;
using System.Threading;
using Drama.Runtime.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Drama.Runtime.Tests
{
    public sealed class DramaPlayerTests
    {
        // ------------------------------------------------------------ 串行

        [Test]
        public void 串行链按顺序执行()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            b.Link(a, bb).Link(bb, c).Link(c);

            var h = new TestHarness();
            var result = h.PlayToEnd(b.Build());

            h.Log.AssertOrder("A", "B", "C");
            Assert.AreEqual(DramaPlayResult.EKind.Completed, result.Kind);
        }

        [Test]
        public void 后继为空即结束()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            b.Mark("从来不该被执行到");
            b.Link(a);

            var h = new TestHarness();
            h.PlayToEnd(b.Build());

            h.Log.AssertOrder("A");
        }

        // ------------------------------------------------------------ 并行 / 汇合

        [Test]
        public void 并行分支汇合后汇合点只执行一次()
        {
            //      ┌→ B ┐
            //  A ──┤    ├→ D → E
            //      └→ C ┘
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            var e = b.Mark("E");
            b.Link(a, bb, c).Link(bb, d).Link(c, d).Link(d, e).Link(e);

            var h = new TestHarness();
            h.PlayToEnd(b.Build());

            Assert.AreEqual(1, h.Log.CountOf("D"), $"汇合点被执行了多次：{h.Log.Trace}");
            Assert.AreEqual(1, h.Log.CountOf("E"));
            Assert.Less(h.Log.IndexOf("B"), h.Log.IndexOf("D"));
            Assert.Less(h.Log.IndexOf("C"), h.Log.IndexOf("D"));
        }

        [Test]
        public void 汇合点要等所有分支真的跑完()
        {
            //      ┌→ GateB ┐
            //  A ──┤        ├→ D
            //      └→ GateC ┘
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var gb = b.Gate("GateB");
            var gc = b.Gate("GateC");
            var d = b.Mark("D");
            b.Link(a, gb, gc).Link(gb, d).Link(gc, d).Link(d);

            var h = new TestHarness();
            var awaiter = h.Start(b.Build());

            // 两条分支都已起跑，都卡在闸门上
            Assert.IsFalse(awaiter.IsCompleted);
            h.Log.AssertOrder("A", "GateB", "GateC");

            h.Gates.Open("GateB");
            Assert.IsFalse(awaiter.IsCompleted, "只放行一条分支，汇合点不该执行");
            Assert.AreEqual(0, h.Log.CountOf("D"));

            h.Gates.Open("GateC");
            Assert.IsTrue(awaiter.IsCompleted);
            Assert.AreEqual(1, h.Log.CountOf("D"));
        }

        [Test]
        public void 分支不等长时也等最慢的那条()
        {
            //      ┌→ B ─────────┐
            //  A ──┤             ├→ E
            //      └→ C → GateD ─┘
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var gd = b.Gate("GateD");
            var e = b.Mark("E");
            b.Link(a, bb, c).Link(bb, e).Link(c, gd).Link(gd, e).Link(e);

            var h = new TestHarness();
            var awaiter = h.Start(b.Build());

            Assert.IsFalse(awaiter.IsCompleted);
            Assert.AreEqual(0, h.Log.CountOf("E"), "短分支跑完了不代表可以进汇合点");

            h.Gates.Open("GateD");
            Assert.IsTrue(awaiter.IsCompleted);
            h.Log.AssertOrder("A", "B", "C", "GateD", "E");
        }

        [Test]
        public void 嵌套并行的内层不会冲出外层汇合点()
        {
            //             ┌→ C ┐
            //      ┌→ B ──┤    ├→ E ┐
            //  A ──┤      └→ D ┘    ├→ G
            //      └→ F ─────────────┘
            //
            // 内层 fork(B) 的汇合点是 E，外层 fork(A) 的汇合点是 G。
            // 内层分支必须同时受两个停止点约束，否则会一路冲过 G。
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            var e = b.Mark("E");
            var f = b.Mark("F");
            var g = b.Mark("G");
            b.Link(a, bb, f).Link(bb, c, d).Link(c, e).Link(d, e).Link(e, g).Link(f, g).Link(g);

            var h = new TestHarness();
            h.PlayToEnd(b.Build());

            Assert.AreEqual(1, h.Log.CountOf("E"), $"内层汇合点跑了多次：{h.Log.Trace}");
            Assert.AreEqual(1, h.Log.CountOf("G"), $"外层汇合点跑了多次：{h.Log.Trace}");
            Assert.Less(h.Log.IndexOf("E"), h.Log.IndexOf("G"));
            Assert.Less(h.Log.IndexOf("F"), h.Log.IndexOf("G"));
        }

        [Test]
        public void 内层并行没有汇合点时各分支跑到底()
        {
            //             ┌→ C (结束)
            //      ┌→ B ──┤
            //  A ──┤      └→ D (结束)
            //      └→ E ───────────→ F
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            var e = b.Mark("E");
            var f = b.Mark("F");
            b.Link(a, bb, e).Link(bb, c, d).Link(c).Link(d).Link(e, f).Link(f);

            var h = new TestHarness();
            var result = h.PlayToEnd(b.Build());

            Assert.AreEqual(DramaPlayResult.EKind.Completed, result.Kind);
            Assert.AreEqual(1, h.Log.CountOf("F"));
            foreach (var name in new[] { "A", "B", "C", "D", "E", "F" })
                Assert.AreEqual(1, h.Log.CountOf(name), $"{name} 执行次数不对：{h.Log.Trace}");
        }

        // ------------------------------------------------------------ 选项分支

        [Test]
        public void 选项分支只走选中的那条()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var left = b.Mark("左");
            var right = b.Mark("右");
            var pick = b.Flow("选", DramaFlowResult.Jump(right));
            b.Link(a, pick).Link(pick).Link(left).Link(right);

            var h = new TestHarness();
            h.PlayToEnd(b.Build());

            h.Log.AssertOrder("A", "选", "右");
        }

        [Test]
        public void 选项分支之后再汇合不会死锁()
        {
            //  这是整套设计要解决的那个问题。
            //
            //      ┌→ 左 ┐
            //  选 ─┤     ├→ 汇合 → 尾
            //      └→ 右 ┘
            //
            //  「汇合」的 InboundCount == 2。如果按静态入边数去等所有入边到齐，
            //  未选的那条分支永远不到，这里就会永久卡住。
            var b = new ScriptBuilder();
            var left = b.Mark("左");
            var right = b.Mark("右");
            var merge = b.Mark("汇合");
            var tail = b.Mark("尾");
            var pick = b.Flow("选", DramaFlowResult.Jump(left));
            b.Entry(pick).Link(pick).Link(left, merge).Link(right, merge).Link(merge, tail).Link(tail);

            var script = b.Build();
            Assert.AreEqual(2, script.Actions[merge].InboundCount, "前提：汇合点确实是静态双入边");

            var h = new TestHarness();
            var result = h.PlayToEnd(script);

            h.Log.AssertOrder("选", "左", "汇合", "尾");
            Assert.AreEqual(DramaPlayResult.EKind.Completed, result.Kind);
        }

        [Test]
        public void 选项没接东西时该条流程直接结束()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var pick = b.Flow("选", DramaFlowResult.Jump(-1));
            b.Link(a, pick).Link(pick);

            var h = new TestHarness();
            var result = h.PlayToEnd(b.Build());

            h.Log.AssertOrder("A", "选");
            Assert.AreEqual(DramaPlayResult.EKind.Completed, result.Kind);
        }

        // ------------------------------------------------------------ 跳转剧本

        [Test]
        public void 跳转剧本返回Goto并停止后续()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var go = b.Flow("跳", DramaFlowResult.Goto(10086));
            var never = b.Mark("不该执行");
            b.Link(a, go).Link(go, never).Link(never);

            var h = new TestHarness();
            var result = h.PlayToEnd(b.Build());

            h.Log.AssertOrder("A", "跳");
            Assert.AreEqual(DramaPlayResult.EKind.Goto, result.Kind);
            Assert.AreEqual(10086, result.GotoDramaId);
        }

        [Test]
        public void 并行分支里发起跳转会掐断其余分支()
        {
            //      ┌→ GateB → 不该执行
            //  A ──┤
            //      └→ 跳(Goto)
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var gate = b.Gate("GateB");
            var never = b.Mark("不该执行");
            var go = b.Flow("跳", DramaFlowResult.Goto(777));
            b.Link(a, gate, go).Link(gate, never).Link(never).Link(go);

            var h = new TestHarness();
            var awaiter = h.Start(b.Build());

            Assert.IsFalse(awaiter.IsCompleted, "还有分支卡在闸门上");
            h.Gates.Open("GateB");

            Assert.IsTrue(awaiter.IsCompleted);
            var result = awaiter.GetResult();
            Assert.AreEqual(DramaPlayResult.EKind.Goto, result.Kind);
            Assert.AreEqual(777, result.GotoDramaId);
            Assert.AreEqual(0, h.Log.CountOf("不该执行"));
        }

        [Test]
        public void 跳转剧本ID非法时按正常结束处理()
        {
            var b = new ScriptBuilder();
            var go = b.Flow("跳", DramaFlowResult.Goto(-1));
            b.Link(go);

            var h = new TestHarness();
            var result = h.PlayToEnd(b.Build());

            Assert.AreEqual(DramaPlayResult.EKind.Completed, result.Kind);
        }

        // ------------------------------------------------------------ 取消 / 边界

        [Test]
        public void 取消时返回Cancelled且不再往下走()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var gate = b.Gate("Gate");
            var never = b.Mark("不该执行");
            b.Link(a, gate).Link(gate, never).Link(never);

            var cts = new CancellationTokenSource();
            var h = new TestHarness();
            var awaiter = h.Start(b.Build(), cts.Token);

            Assert.IsFalse(awaiter.IsCompleted);
            cts.Cancel();

            Assert.IsTrue(awaiter.IsCompleted);
            Assert.AreEqual(DramaPlayResult.EKind.Cancelled, awaiter.GetResult().Kind);
            Assert.AreEqual(0, h.Log.CountOf("不该执行"));
        }

        [Test]
        public void 入口非法时直接完成()
        {
            var b = new ScriptBuilder();
            b.Mark("A");
            b.Entry(-1);

            var h = new TestHarness();
            var result = h.PlayToEnd(b.Build());

            Assert.AreEqual(0, h.Log.Count);
            Assert.AreEqual(DramaPlayResult.EKind.Completed, result.Kind);
        }

        [Test]
        public void 没注册Handler时抛出可读的异常()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            b.Link(a);

            var registry = new DramaHandlerRegistry();   // 空的
            var player = new DramaPlayer(registry);

            Assert.Throws<DramaMissingHandlerException>(() =>
                player.PlayAsync(b.Build(), new TestContext(), default).GetAwaiter().GetResult());
        }

        [Test]
        public void 播放前能查出缺失的Handler()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            b.Gate("G");
            b.Link(a);

            var registry = new DramaHandlerRegistry().Register(new MarkHandler(new ExecutionLog()));
            var missing = registry.FindMissing(b.Build());

            CollectionAssert.AreEqual(new[] { typeof(GateAction) }, missing);
        }

        [Test]
        public void 死循环会被步数上限拦住()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            b.Link(a, a);   // 自己指向自己

            var h = new TestHarness();
            h.Player.MaxSteps = 500;

            Assert.Throws<System.InvalidOperationException>(() =>
                h.Player.PlayAsync(b.Build(), h.Context, default).GetAwaiter().GetResult());
        }

        [Test]
        public void 格式版本过高时拒绝播放()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            b.Link(a);
            var script = b.Build();
            script.FormatVersion = DramaScript.CurrentFormatVersion + 1;

            var h = new TestHarness();
            Assert.Throws<System.NotSupportedException>(() =>
                h.Player.PlayAsync(script, h.Context, default).GetAwaiter().GetResult());
        }

        // ------------------------------------------------------------ 回调

        [Test]
        public void ActionExecuting按执行顺序触发()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            b.Link(a, bb).Link(bb);

            var h = new TestHarness();
            var seen = new System.Collections.Generic.List<string>();
            h.Player.ActionExecuting += act => seen.Add(((MarkAction)act).Name);

            h.PlayToEnd(b.Build());

            CollectionAssert.AreEqual(new[] { "A", "B" }, seen);
        }

        // ------------------------------------------------------------ 读档恢复

        [Test]
        public void 恢复会重放存档点之前的每一条指令()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            b.Link(a, bb).Link(bb, c).Link(c);

            var h = new TestHarness();
            h.PlayRestoring(b.Build(), restoreUntilIndex: c);

            // 不是"跳到 C"，而是 A、B 也照跑一遍 —— 舞台就是靠它们堆回来的
            h.Log.AssertOrder("A", "B", "C");
        }

        [Test]
        public void 恢复期间是Restoring模式走到存档点就还原()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            b.Link(a, bb).Link(bb, c).Link(c, d).Link(d);

            var h = new TestHarness();
            h.Context.Mode = EDramaPlaybackMode.Auto;   // 玩家原来开着自动
            h.PlayRestoring(b.Build(), restoreUntilIndex: c);

            Assert.AreEqual(EDramaPlaybackMode.Restoring, h.ModeAt(a), "A 应该是被静默重放的");
            Assert.AreEqual(EDramaPlaybackMode.Restoring, h.ModeAt(bb), "B 应该是被静默重放的");

            // 存档点这一条自己就得正常执行 —— 它是"停在这句台词等玩家"的那一条
            Assert.AreEqual(EDramaPlaybackMode.Auto, h.ModeAt(c), "存档点应当已经切回原模式");
            Assert.AreEqual(EDramaPlaybackMode.Auto, h.ModeAt(d));
            Assert.AreEqual(EDramaPlaybackMode.Auto, h.Context.Mode, "跑完模式必须还是玩家原来那个");
        }

        [Test]
        public void 存档点下标越界时从头正常播()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            b.Link(a, bb).Link(bb);

            var h = new TestHarness();
            h.Context.Mode = EDramaPlaybackMode.Normal;

            // 剧本改过、下标不再有效。这里不做任何迁移，就当没这回事
            h.PlayRestoring(b.Build(), restoreUntilIndex: 999);

            h.Log.AssertOrder("A", "B");
            Assert.AreEqual(EDramaPlaybackMode.Normal, h.ModeAt(a), "越界时不该进恢复模式");
            Assert.AreEqual(EDramaPlaybackMode.Normal, h.Context.Mode);
        }

        [Test]
        public void 存档点在走不到的地方时模式也要还原()
        {
            // C 是孤立的：下标合法，但从入口顺着连线永远到不了
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            b.Link(a, bb).Link(bb);

            var h = new TestHarness();
            h.Context.Mode = EDramaPlaybackMode.Normal;

            LogAssert.Expect(LogType.Warning, new Regex("没走到那一条"));
            h.PlayRestoring(b.Build(), restoreUntilIndex: c);

            // 这条不还原的话，后面整段剧情会以 Restoring 的速度冲完，
            // 表现是"读档后剧情一闪而过"，而且很难看出是模式没还
            Assert.AreEqual(EDramaPlaybackMode.Normal, h.Context.Mode, "没到达目标也必须把模式还回去");
        }

        [Test]
        public void 恢复点落在并行分支里也能重放到位()
        {
            //      ┌→ B ┐
            //  A ──┤    ├→ D
            //      └→ C ┘
            // 存档点在分支 C 上 —— 直接从 C 起一个新 Runner 会丢掉 B 和汇合语义，
            // 重放则是照原结构走，两个问题一起解决
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            b.Link(a, bb, c).Link(bb, d).Link(c, d).Link(d);

            var h = new TestHarness();
            h.PlayRestoring(b.Build(), restoreUntilIndex: c);

            Assert.AreEqual(4, h.ModeTrace.Count, $"每条都该跑到一次：{h.Log.Trace}");
            Assert.AreEqual(EDramaPlaybackMode.Normal, h.ModeAt(c), "存档点应当已经切回原模式");
            Assert.AreEqual(EDramaPlaybackMode.Normal, h.ModeAt(d), "汇合点在存档点之后，应当正常执行");
        }
    }
}
