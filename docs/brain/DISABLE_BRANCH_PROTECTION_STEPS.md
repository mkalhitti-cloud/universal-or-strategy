# How to Disable Branch Protection to Delete Branches

## Current Situation
You have a ruleset called **"Src-Only PR Enforcement"** that is blocking branch deletion.

**Rule Details**:
- **Status**: Active (green indicator)
- **Applies to**: 85 branches (all branches matching `**/*`)
- **Contains**: 2 branch rules

## Steps to Temporarily Disable

### Option 1: Disable the Entire Ruleset (Fastest)

1. **You're already on the right page**: https://github.com/antigravityos187-sketch/universal-or-strategy/rules

2. **Click on "Src-Only PR Enforcement"** (the green row)

3. **Scroll down to "Enforcement status"** section

4. **Change from "Active" to "Disabled"**
   - Click the dropdown that currently says "Active"
   - Select "Disabled"

5. **Click "Save changes"** at the bottom

6. **Delete your branches** (via command line or GitHub UI)

7. **Re-enable the ruleset**:
   - Go back to the same page
   - Change "Disabled" back to "Active"
   - Click "Save changes"

### Option 2: Modify the Rule to Exclude Merged Branches

1. Click on **"Src-Only PR Enforcement"**

2. Scroll to **"Target branches"** section

3. **Add exclusion patterns** for branches you want to delete:
   - Click "Add target" or modify existing patterns
   - Add exclusions like:
     - `!infra/epic-posinfo-*`
     - `!docs/jane-street-*`

4. **Save changes**

5. **Delete branches** (they're now excluded from the rule)

6. **Remove exclusions** after deletion (optional)

## Recommended Approach

**Use Option 1** (Disable temporarily) because:
- ✅ Faster - just toggle a switch
- ✅ Cleaner - no pattern modifications needed
- ✅ Safer - you'll remember to re-enable it

## After Disabling the Rule

Run these commands to delete the 2 merged branches on GitHub:

```bash
# Delete the infrastructure branch
git push origin --delete infra/epic-posinfo-phase1.5-docs

# Delete the Jane Street docs branch
git push origin --delete docs/jane-street-deviations-3-4
```

**Expected Result**: GitHub branch count drops from 85 to 83.

## Re-Enable Protection

After deleting branches:
1. Go back to: https://github.com/antigravityos187-sketch/universal-or-strategy/rules
2. Click "Src-Only PR Enforcement"
3. Change "Disabled" back to "Active"
4. Click "Save changes"

## Why This Rule Exists

The "Src-Only PR Enforcement" rule likely prevents:
- Direct pushes to protected branches
- Branch deletion without review
- Accidental removal of important branches

It's a good rule to have - we just need to temporarily disable it for cleanup.