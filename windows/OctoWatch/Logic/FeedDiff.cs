using System.Collections.ObjectModel;

namespace OctoWatch;

/// <summary>
/// Reconciles the ListView's bound ObservableCollection with the desired list
/// in place, touching only what changed. This keeps a refresh/poll from
/// re-rendering the whole list: containers for unchanged items are preserved,
/// so scroll position holds and the "running" pulse animation doesn't restart.
/// </summary>
public static class FeedDiff
{
    public static void Apply(ObservableCollection<FeedItem> target, IReadOnlyList<FeedItem> desired)
    {
        // 1) Drop items that are gone (matched by stable identity).
        var desiredIds = new HashSet<string>(desired.Count);
        foreach (var item in desired)
            desiredIds.Add(FeedMapper.Identity(item));

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!desiredIds.Contains(FeedMapper.Identity(target[i])))
                target.RemoveAt(i);
        }

        // 2) Align order and content.
        for (var i = 0; i < desired.Count; i++)
        {
            var want = desired[i];
            var wantId = FeedMapper.Identity(want);

            if (i < target.Count && FeedMapper.Identity(target[i]) == wantId)
            {
                // Same slot: replace only if the content changed (record value equality).
                if (!target[i].Equals(want))
                    target[i] = want;
                continue;
            }

            // Present elsewhere? Move it here (and update if needed).
            var found = -1;
            for (var j = i + 1; j < target.Count; j++)
            {
                if (FeedMapper.Identity(target[j]) == wantId)
                {
                    found = j;
                    break;
                }
            }

            if (found >= 0)
            {
                if (!target[found].Equals(want))
                    target[found] = want;
                target.Move(found, i);
            }
            else
            {
                target.Insert(i, want);
            }
        }

        // 3) Trim leftovers at the end.
        while (target.Count > desired.Count)
            target.RemoveAt(target.Count - 1);
    }
}
