using Drama.Runtime.Flow;
using NUnit.Framework;

namespace Drama.Runtime.Tests
{
    /// <summary>汇合点计算的测试。这层错了，执行器再对也没用。</summary>
    public sealed class DramaScriptIndexTests
    {
        [Test]
        public void 串行链没有汇合点()
        {
            var b = new ScriptBuilder();
            var a0 = b.Mark("A");
            var a1 = b.Mark("B");
            b.Link(a0, a1).Link(a1);

            var index = DramaScriptIndex.Build(b.Build());

            Assert.AreEqual(DramaScriptIndex.NoJoin, index.JoinOf(a0));
            Assert.AreEqual(DramaScriptIndex.NoJoin, index.JoinOf(a1));
        }

        [Test]
        public void 菱形结构能算出汇合点()
        {
            //      ┌→ B ┐
            //  A ──┤    ├→ D
            //      └→ C ┘
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            b.Link(a, bb, c).Link(bb, d).Link(c, d).Link(d);

            var index = DramaScriptIndex.Build(b.Build());

            Assert.AreEqual(d, index.JoinOf(a));
        }

        [Test]
        public void 分支长度不一时汇合点仍然正确()
        {
            //      ┌→ B ───────┐
            //  A ──┤           ├→ E
            //      └→ C → D ───┘
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            var e = b.Mark("E");
            b.Link(a, bb, c).Link(bb, e).Link(c, d).Link(d, e).Link(e);

            var index = DramaScriptIndex.Build(b.Build());

            Assert.AreEqual(e, index.JoinOf(a));
        }

        [Test]
        public void 汇合点取最早的那个而不是随便一个共同后继()
        {
            //      ┌→ B ┐
            //  A ──┤    ├→ D → E → F
            //      └→ C ┘
            // D/E/F 都在两条分支的可达交集里，必须挑 D
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var d = b.Mark("D");
            var e = b.Mark("E");
            var f = b.Mark("F");
            b.Link(a, bb, c).Link(bb, d).Link(c, d).Link(d, e).Link(e, f).Link(f);

            var index = DramaScriptIndex.Build(b.Build());

            Assert.AreEqual(d, index.JoinOf(a));
        }

        [Test]
        public void 分支永不重逢时返回NoJoin()
        {
            //      ┌→ B (结束)
            //  A ──┤
            //      └→ C (结束)
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            b.Link(a, bb, c).Link(bb).Link(c);

            var index = DramaScriptIndex.Build(b.Build());

            Assert.AreEqual(DramaScriptIndex.NoJoin, index.JoinOf(a));
            Assert.IsNotEmpty(index.Diagnostics, "应该给出一条『各分支没有共同汇合点』的提示");
        }

        [Test]
        public void 选项分支的跳转目标算进可达性()
        {
            // A → Choice ┬→ B
            //            └→ C
            // Choice 自己的 Next 是空的，目标全在 Options 里。
            // 如果 CollectBranchTargets 没接上，B/C 就会被当成不可达的死代码。
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            var choice = b.Choice("选", bb, c);
            b.Link(a, choice).Link(choice).Link(bb).Link(c);

            var index = DramaScriptIndex.Build(b.Build());

            CollectionAssert.AreEquivalent(new[] { bb, c }, index.SuccessorsOf(choice));
            Assert.GreaterOrEqual(index.DepthOf(bb), 0, "B 应该从入口可达");
            Assert.GreaterOrEqual(index.DepthOf(c), 0, "C 应该从入口可达");
        }

        [Test]
        public void 越界后继被剔除并记进诊断()
        {
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            b.Link(a, 99);

            var index = DramaScriptIndex.Build(b.Build());

            CollectionAssert.IsEmpty(index.SuccessorsOf(a));
            Assert.IsNotEmpty(index.Diagnostics);
        }

        [Test]
        public void 有环的图不会把可达性算爆()
        {
            // A → B → C → A
            var b = new ScriptBuilder();
            var a = b.Mark("A");
            var bb = b.Mark("B");
            var c = b.Mark("C");
            b.Link(a, bb).Link(bb, c).Link(c, a);

            var index = DramaScriptIndex.Build(b.Build());

            Assert.AreEqual(0, index.DepthOf(a));
            Assert.AreEqual(1, index.DepthOf(bb));
            Assert.AreEqual(2, index.DepthOf(c));
        }
    }
}
