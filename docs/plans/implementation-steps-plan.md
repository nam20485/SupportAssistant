I really like these documents:
- `docs/plans/inference-integration-implementation-plan.md`
- `docs/plans/ws1-inference-diagnostics-development-plan.md`

Lets create a skill  `/implementation-steps` that has 4 stages, corresponding to the 3 documents + the implementation of the 3rd WSx doc, specified by the an argument `$stage`

`get-status`: --> `docs/plans/inference-engine-integration-status.md`
`plan`: --> `docs/plans/inference-integration-implementation-plan.md`
`ws-plan`: --> `docs/plans/ws1-inference-diagnostics-development-plan.md`
`develop-ws`: implemen the wsN doc that was jus created  (takes a 2nd argument `$workstream-plan`)

I want the structure copied out of the existing documennts (listed above) so create templates wityh help descriptions and examples.

I really like the "Workstream overview & sequencing" and "Progress" setions in the -plan doc, make sure to describe that in the docs andinstruct clearly in the develop-ws skill stage to keep that updated as implementation proceeds well as check it when starting" Make it clear that progress and notes should be updated and added to track progress as its made.

Also good is the structure of how the overall workstrems are laid out laid in the 2nd doc and then the given WS is broken down into its tasks in the 3rd wsN- doc. Make sure to kepp that relation.

Create another seciton under each WSn section (in the -plan)  for implementaiton notes to fill out after that WS has been implmented.

If its easier to create 4 separate skill instead of one skill with differtent args do that.

User may skip the first stage if not needed bc its already clear exactly what needs to be implemented. If it is- you'll need to gather context in the beginning of the 2nd stage (which is good practive either way)
