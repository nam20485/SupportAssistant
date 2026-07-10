I really like the stucture and content in these two documents:
- `docs/plans/inference-integration-implementation-plan.md`
- `docs/plans/ws1-inference-diagnostics-development-plan.md`

Create a new skill `/implementation-steps` that has 4 stages, corresponding to the 3 documents + the implementation of the 3rd WSx doc.

- The specified stage is passed as an argument `$stage`

The first three stages create the corresponding documents.
The 4th stage executes the implementation of the 3rd WSx doc that was just created.

`get-status`: --> `docs/plans/inference-engine-integration-status.md`
`plan`: --> `docs/plans/inference-integration-implementation-plan.md`
`ws-plan`: --> `docs/plans/ws1-inference-diagnostics-development-plan.md`
`develop-ws`: implement the wsN doc that was just created  (takes a 2nd argument `$workstream-plan`)

I especially like the following secitns so make sure copy these aprts so they are present in the new tempaltes and skills:

- Overall strucutre: I want the structure copied out of the existing documennts (listed above) so create templates with helpful descriptions and examples.
- "Workstream overview & Sequencing" and "Progress" setions in the -plan doc (make sure to describe that in the docs and )instruct clearly in the develop-ws skill stage to keep that updated as implementation proceeds well as check it when starting)
- Make it clear that progress and notes should be updated and added to track progress as its made.
- Structure of how the overall workstrems are laid out in the 2nd doc and then the given WS's is broken down into its constituent tasks in the 3rd wsN- doc.

Add another seciton under each WSn section (in the -plan) for implementaiton notes to fill out after that WSn has been implmented.

If its easier to create 4 separate skill instead of one skill with differtent args do that.

User may skip the first stage if not needed bc its already clear exactly what needs to be implemented. If it is- you'll need to gather context in the beginning of the 2nd stage (which is good practive either way)


Read and follow the skills specification and docs.
Skills docs: <https://agentskills.io/home.md>