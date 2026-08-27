using System.Collections.ObjectModel;

namespace OctoWatch;

/// <summary>
/// Patches an ObservableCollection in place so ListView item containers stay
/// alive across refresh (scroll position and the running-dot animation survive).
/// </summary>
public static class FeedDiff
{
    public static void Apply(ObservableCollection<FeedItem> target, IReadOnlyList<FeedItem> desired)
    {
        // Drop items that disappeared (stable identity).
        var desiredIds = new HashSet<string>(desired.Count);
        foreach (var item in desired)
            desiredIds.Add(FeedMapper.Identity(item));

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!desiredIds.Contains(FeedMapper.Identity(target[i])))
                target.RemoveAt(i);
        }

        // Align order and content.
        for (var i = 0; i < desired.Count; i++)
        {
            var want = desired[i];
            var wantId = FeedMapper.Identity(want);

            if (i < target.Count && FeedMapper.Identity(target[i]) == wantId)
            {
                // Same slot: replace only when the record value changed.
                if (!target[i].Equals(want))
                    target[i] = want;
                continue;
            }

            // Present later in the list? Move it here.
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

        // Trim leftovers at the end.
        while (target.Count > desired.Count)
            target.RemoveAt(target.Count - 1);
    }
}
