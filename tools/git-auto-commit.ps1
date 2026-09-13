# Periodic snapshot commit/push for solo WIP backup.
# Runs on a schedule (see README.md for how it's registered). Does nothing
# if there's nothing to commit, so it's safe to run as often as you like.

$repoPath = "E:\DotSoccer"
Set-Location $repoPath

$status = git status --porcelain
if (-not $status) {
    exit 0
}

git add -A
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"
git commit -m "Auto-commit: periodic snapshot ($timestamp)"
git push origin master
