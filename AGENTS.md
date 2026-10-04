# Superseded checkout — read before working

`/run/media/ekwav/Data/dev/Connections` is the historical Angular checkout.
It is superseded by the Flutter application in https://github.com/Coflnet/ConUi.

For Con feature work, fixes, tests or deployment:

1. Work in `/run/media/ekwav/Data/dev/Con/ConUi-work/integration`.
2. Use branch `integration/stories-map-rollout` and read `HANDOFF.md` there first.
3. Keep `/run/media/ekwav/Data/dev/Con/ConUi` untouched: it is the owner's checkout
   with local work.

Do not implement, build or deploy Con from this historical checkout unless the
owner explicitly requests maintenance of the archived Angular project. Preserve
its existing staged, unstaged and untracked work; do not reset, clean or delete it,
or include it in a commit for another task.
