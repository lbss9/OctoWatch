using System.Collections.ObjectModel;

namespace OctoWatch;

/// <summary>
/// Reconcilia (in-place) uma ObservableCollection ligada à ListView com a lista
/// desejada, mexendo só no que mudou. Assim o refresh/polling NÃO re-renderiza a
/// fila inteira: containers de itens inalterados são preservados (scroll mantido,
/// a animação da bolinha "em execução" não reinicia).
/// </summary>
public static class FeedDiff
{
    public static void Apply(ObservableCollection<FeedItem> target, IReadOnlyList<FeedItem> desired)
    {
        // 1) Remove o que sumiu (por identidade estável).
        var desiredIds = new HashSet<string>(desired.Count);
        foreach (var item in desired)
            desiredIds.Add(FeedMapper.Identity(item));

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!desiredIds.Contains(FeedMapper.Identity(target[i])))
                target.RemoveAt(i);
        }

        // 2) Alinha ordem e conteúdo.
        for (var i = 0; i < desired.Count; i++)
        {
            var want = desired[i];
            var wantId = FeedMapper.Identity(want);

            if (i < target.Count && FeedMapper.Identity(target[i]) == wantId)
            {
                // Mesma posição: substitui só se o conteúdo mudou (record == por valor).
                if (!target[i].Equals(want))
                    target[i] = want;
                continue;
            }

            // Existe em outra posição? Move para cá (e atualiza se preciso).
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

        // 3) Remove sobras no fim.
        while (target.Count > desired.Count)
            target.RemoveAt(target.Count - 1);
    }
}
