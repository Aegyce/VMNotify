using System.Text.Json;
using VMNotify;

internal static class FlashingTests
{
    public static void Run() {
        static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        var clock = new TimingTests.Clock();
        var receiver = new Receiver(clock);
        var rule = new NotificationRule { Condition = RuleCondition.IconEquals, Target = "pix-v1-alert",
            FlashingNotification = true, RepeatSeconds = 10 };
        var alert = new IconObservation("tray", "pix-v1-alert", null, true, [], false, false);
        var normal = alert with { Fingerprint = "pix-v1-normal", Colorful = false };
        AgentEvent Sample(IconObservation icon) => new("heartbeat", "lanxin", "蓝信", Icon: icon);
        receiver.SetEnabledApps(["lanxin"]); receiver.SetRules(new() { ["lanxin"] = rule });
        receiver.ApplyObservation(Sample(alert));
        Check(receiver.Notifications.Reader.TryRead(out var first) && receiver.ShouldDisplay(first), "initial flashing frame notifies");
        // Delivery time, rather than sampling or frame transitions, starts the interval.
        clock.Advance(TimeSpan.FromSeconds(1)); receiver.MarkDelivered(first!);
        for (int i = 1; i <= 39; i++) {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            receiver.ApplyObservation(Sample(i % 2 == 0 ? alert : normal)); receiver.QueueReminders();
            Check(receiver.ShouldDisplay(first!) && !receiver.Notifications.Reader.TryRead(out _), "alternating frames must not restart or bypass repeat interval");
        }
        clock.Advance(TimeSpan.FromMilliseconds(250)); receiver.ApplyObservation(Sample(normal)); receiver.QueueReminders();
        Check(receiver.Notifications.Reader.TryRead(out var repeat) && repeat.Kind == "reminder" && receiver.ShouldDisplay(repeat), "flashing uses the existing ten-second interval");
        Check(repeat!.Message == "蓝信 图标变为彩色", "a temporary normal frame must not rewrite flashing reminder text");
        receiver.MarkDelivered(repeat!);
        receiver.ApplyObservation(Sample(alert));
        receiver.ApplyObservation(Sample(normal)); clock.Advance(TimeSpan.FromMilliseconds(2999)); receiver.ApplyObservation(Sample(normal));
        Check(receiver.ShouldDisplay(repeat!), "short normal pause remains the same round");
        clock.Advance(TimeSpan.FromMilliseconds(1)); receiver.ApplyObservation(Sample(normal));
        Check(!receiver.ShouldDisplay(repeat!), "three seconds of confirmed normal ends the round");
        receiver.ApplyObservation(Sample(alert));
        Check(receiver.Notifications.Reader.TryRead(out var next) && next.Kind == "attention", "confirmed recovery permits a new round immediately");

        receiver.SetRules(new() { ["lanxin"] = rule with { RepeatSeconds = 0 } });
        while (receiver.Notifications.Reader.TryRead(out _)) { }
        for (int i = 0; i < 200; i++) {
            clock.Advance(TimeSpan.FromMilliseconds(250)); receiver.ApplyObservation(Sample(i % 2 == 0 ? alert : normal)); receiver.QueueReminders();
        }
        Check(!receiver.Notifications.Reader.TryRead(out _), "zero interval is one notification per flashing round");
        receiver.ApplyObservation(Sample(alert with { Fingerprint = null }));
        receiver.ApplyObservation(Sample(alert));
        Check(receiver.Notifications.Reader.TryRead(out next), "unknown telemetry ends round rather than masking data loss");
        receiver.ApplyObservation(Sample(alert with { Removed = true }));
        Check(!receiver.ShouldDisplay(next!), "removed icon immediately cancels the round");

        var hold = new RuleState(); var heldRule = rule with { HoldSeconds = 2 };
        for (int i = 0; i <= 8; i++) {
            Check(hold.Update(heldRule, i % 2 == 0 ? alert : normal, clock) == (i == 8), "flashing hold spans alternating frames");
            clock.Advance(TimeSpan.FromMilliseconds(250));
        }
        var suppressed = new RuleState(); var initialOff = rule with { NotifyInitial = false };
        for (int i = 0; i < 40; i++) {
            Check(!suppressed.Update(initialOff, i % 2 == 0 ? alert : normal, clock), "initial suppression covers the whole flashing round");
            clock.Advance(TimeSpan.FromMilliseconds(250));
        }
        suppressed.Update(initialOff, normal, clock); clock.Advance(TimeSpan.FromSeconds(3)); suppressed.Update(initialOff, normal, clock);
        Check(suppressed.Update(initialOff, alert, clock), "initial suppression rearms after sustained recovery");

        var old = JsonSerializer.Deserialize<NotificationRule>("{}");
        Check(old?.FlashingNotification == false, "existing settings retain ordinary behavior");
        Check(JsonSerializer.Deserialize<NotificationRule>(JsonSerializer.Serialize(rule)) == rule, "flashing option persists");
        var ordinary = new RuleState();
        Check(ordinary.Update(rule with { FlashingNotification = false }, alert, clock)
            && !ordinary.Update(rule with { FlashingNotification = false }, normal, clock), "unchecked rules recover immediately");
        foreach (var condition in new[] { RuleCondition.Default, RuleCondition.Flashing, RuleCondition.NeedsAttention }) {
            var state = new RuleState(); var signalRule = rule with { Condition = condition };
            var on = alert with { Flashing = condition != RuleCondition.NeedsAttention, Attention = condition == RuleCondition.NeedsAttention };
            Check(state.Update(signalRule, on, clock) && state.Update(signalRule, normal, clock), "signal rules share flashing round handling");
        }
        Console.WriteLine("PASS: configurable flashing rounds, shared repeat interval, initial suppression, hold and recovery, ordinary defaults and telemetry loss.");
    }
}
