# Windows desktop acceptance

Run on Windows with a new temporary `FAMILYTREE_DB_PATH`, using the README startup command or the published `FamilyTree.UI.exe`. This checklist is a manual platform check; Linux tests and cross-builds do not execute it.

1. Start the app. Confirm the empty-state message and that Add member is available while edit/delete/relationship actions are disabled without a selection.
2. Add John (Male), Mary (Female), Michael (Male), and Anna (Female). Leave one birth date unknown. Verify invalid date text, a future date, and an empty name produce an error without saving; correct each and save successfully.
3. Select John and add Husband of Mary. Confirm both members display Married, John sees Mary as Wife, and Mary sees John as Husband.
4. Add John as Father and Mary as Mother of both children. Alternatively enter Michael as Son and Anna as Daughter of each parent. Confirm reverse labels and each child's sibling entry appear once.
5. Attempt another father for Anna, a self-link, a reversed duplicate spouse, and a parent cycle. Confirm helpful errors and unchanged persisted relationships. Change Mary's gender to Male and confirm the conflicting parent edit is rejected.
6. Add a grandparent and verify grandparent/grandchild and ancestor/descendant displays. Try linking an ancestor as a current/former spouse and confirm rejection.
7. Select the John–Mary spouse relationship and choose Mark as former spouse. Confirm the confirmation dialog, both Divorced statuses, and Ex-wife/Ex-husband labels. Restart and verify they persist.
8. Add a different current spouse for John, then add another historical former spouse. Confirm John's current Married state is preserved. Reject a second active spouse.
9. Cancel a relationship-removal confirmation. Verify no change. Confirm removal of an incorrect spouse fact and check the remaining facts determine statuses. Confirm parent deletion is blocked while relationships exist; remove those relationships and then delete the member.
10. Search for a member, rename them, and save. Confirm they remain selected and visible. Cancel edits and ensure they are not saved. Navigate using the keyboard and inspect the form at 100% and 150% Windows scaling.
11. Close and reopen the application and check all saved data. Launch the self-contained package on a supported Windows x64 machine without installing .NET; confirm the same workflow and native SQLite load successfully.

Record the Windows version, SDK/package used, results, and any failed step before calling desktop validation complete.
