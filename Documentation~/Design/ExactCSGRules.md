# Exact CSG: the rules

Every change to Chisel's CSG follows these rules. A plan that breaks one is wrong, however good the reason looks. They
come from the design section of [ExactCSG.md](ExactCSG.md), the conclusion of [CanonicalVertices.md](CanonicalVertices.md)
and the principles in [ExactPredicates.md](ExactPredicates.md), and from corrections that had to be made more than once.

## The algorithm

1. **The input is the transformed planes.** Each brush's planes, transformed into tree space, as they come out. The
   CSG never blames transforms, generators or importers, and never changes them to fix itself. Generators give planes,
   and the CSG does everything else from them (Sander, 27 September 2026: "The generators should just give planes to
   the CSG algorithm, the CSG algorithm then should do whatever it needs to do to make it work"): no brush is rejected
   over a float outline, and no plane is re-derived from one.
2. **Each plane is snapped once, on its own** (`ExactPlane.Quantize`): its unit normal to multiples of 2^-20 and its
   offset to multiples of 2^-14 units, which makes it exact integers. The grid is fixed, one for every tree, and was
   measured once ([ExactCSG.md](ExactCSG.md), "The snap"). Planes are never merged and a plane never looks at another
   brush's planes: two planes become one only by snapping to the same integers.
3. **Only plane intersections.** Every vertex is an intersection of planes, and every decision is an exact sign test
   on planes. No distance, tolerance or epsilon is used anywhere in the CSG, including the broad phase and bounds.
4. **More than three planes can meet at a vertex.** A vertex is an exact point, identified by the point, never by the
   three planes it happened to be computed from.
5. **Output is rounded to float32 once, at the end.**
6. **Vertices are welded, and slivers cleaned up, only at the very end**, on each model's whole output mesh. Nothing
   done there feeds back into a CSG decision.
7. **Any model a user builds.** Any generator, any convex brush, any transform, any operation and any nesting, and it
   has to scale to large levels. Nothing is importer-specific; imported maps are test data and nothing more.
8. **Never use Quake/Source BSP compilers as a source**, whether as a model, a precedent or an explanation. They are
   known for their failure cases.
9. **No made-up techniques and no heuristics.** Everything rests on the written design and on the geometric-robustness
   literature.

## How the work is done

10. **Follow the written design.** Re-read it and this file after every compaction and before every decision, and
    check each plan against it rule by rule.
11. **Don't wing it.** Derive each step, test it, and prove the test can fail by planting the error it guards against.
    Every bug starts as a minimal failing test.
12. **Tests judge exactly**, on the same planes the CSG used. Never tune the CSG to satisfy a checker that uses
    tolerances; fix the checker.
13. **A correction is recorded and followed.** Write it into memory and act on it. Never answer "you're right" and then
    do something else.
14. **Notes and memory stay current**, so a compaction loses nothing.
15. **Editor, git and agents.** One editor slot at a time under the editor lock, slot scripts strictly one after
    another. Never kill all Unity instances. Commit by path, without touching other people's files. Never push. No
    sub-agents.

## Mistakes already made

Each of these was made and had to be corrected. Don't make them again.

- A weld that merged nearly coplanar faces of different brushes by distance and angle (`ExactPlaneWeld`). It was made
  up, it is a tolerance, and it breaks rules 2 and 3.
- Raising that weld to τ, the old fat-plane width, to make a checker with tolerances pass. That breaks rule 12.
- Proposing to make transforms, generators and importers exact. That breaks rule 1.
- Proposing to measure a coarser snap grid, on 24 September, was recorded here as the weld again. Sander decided
  otherwise on 26 September: "we need to snap planes, like we planned, instead of modifying generators". The snap
  rounds each plane on its own and compares no planes; the weld clustered the planes of different brushes by distance.
  Rule 2 now says what the snap is.
- Proposing to make the generators hand over exact planes (26 September). That is rule 1 again: planes are snapped
  instead.
- Citing BSP compilers as a precedent. That breaks rule 8.
- Describing a vertex as a triple of planes. That breaks rule 4.
