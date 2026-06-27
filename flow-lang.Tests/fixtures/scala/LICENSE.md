# Scala Tuning Fixtures — Credits

The 5 canonical `.scl` files in this directory are sourced from the
**Huygens-Fokker Foundation Scala scale archive** (Manuel Op de Coul, curator).

## Fixture-to-Archive Mapping

| In-repo fixture       | Original archive filename                                                                                                            | Source URL                                                                                          |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------- |
| `partch_43.scl`       | `partch_43.scl`                                                                                                                      | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/partch_43.scl             |
| `slendro.scl`         | `slendro.scl`                                                                                                                        | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/slendro.scl               |
| `carlos_alpha.scl`    | `carlos_alpha.scl`                                                                                                                   | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/carlos_alpha.scl          |
| `pythagorean_12.scl`  | `pyth_12.scl` (renamed for clarity; content verbatim — 12-tone Pythagorean scale)                                                    | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/pyth_12.scl               |
| `just_5limit.scl`     | `ji_12.scl` (renamed for clarity; content verbatim — Robert Rich's "Basic JI with 7-limit tritone", 5-limit on the diatonic)         | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/ji_12.scl                 |

Each renamed file additionally carries an `! ORIGINAL ARCHIVE FILENAME: …`
comment as its first line so the rename audit trail is visible inside the
file itself, independent of this LICENSE.md (per Phase 32 decision D-16).

## Attribution

The Scala scale archive is maintained by Manuel Op de Coul
(coul@huygens-fokker.org) and the Huygens-Fokker Foundation. The archive's
~5350 files are released for free use per the long-standing community
understanding documented on https://www.huygens-fokker.org/scala/. Files
sourced from the archive in this directory are verbatim copies — cents
values, ratios, descriptions, step counts, and comment lines are unchanged
relative to the upstream archive (the only addition is the one-line
`! ORIGINAL ARCHIVE FILENAME: …` audit comment prepended to the two renamed
files listed above).

The wording above follows the softened-community-use formulation locked by
Phase 32 decision D-17. The Huygens-Fokker Foundation's downloads page
(https://www.huygens-fokker.org/scala/downloads.html) does not carry an
explicit free-software licence statement; the archive's free-use status is a
long-standing community understanding rather than a formal grant. If your
project requires a stricter licensing posture, contact Manuel Op de Coul
directly.

## Hand-Authored Negative-Case Fixtures

The 3 negative-case fixtures (`malformed_step_count.scl`,
`malformed_cents.scl`, `malformed_kbm.kbm`) are hand-authored minimal repros
for SPEC-7 parser error-path tests. Each isolates exactly one class of
error at column 1 so the parser's `{file}:{line}:{col}` diagnostic text is
unambiguous. These fixtures are released under the same terms as the Flow
project itself.
