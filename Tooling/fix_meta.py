from pathlib import Path
import re


def main():
    source = Path(r"../../SPT41/AssetDump/AssetRipper_export_20260925_152456/ExportedProject/Assets/Scripts/Assembly-CSharp")
    target = Path(r"../../SPT41/ScriptsSDK/Assets/SDK/Assembly-CSharp")

    guids = {}
    fixed = set()

    # first fix files with matching relative paths

    for meta_file in source.rglob("*.meta"):
        text = meta_file.read_text(encoding="utf-8")
        match = re.search(r"guid:\s*(\S+)", text)
        if match:
            relative_path = str(meta_file.relative_to(source))
            guid = match.group(1)
            guids[relative_path] = guid

    for meta_file in target.rglob("*.meta"):
        relative_path = str(meta_file.relative_to(target))
        if relative_path not in guids:
            continue

        new_guid = guids[relative_path]
        text = meta_file.read_text(encoding="utf-8")
        new_text = re.sub(r"guid:\s*\S+", f"guid: {new_guid}", text, count=1)
        meta_file.write_text(new_text, encoding="utf-8")
        fixed.add(relative_path)

    # but some files are in different folders, so fix them by names,
    # but there are duplicates, so we remove them because I dont want
    # to decide which is which from python script

    guids_by_name = {}
    duplicate_names = set()

    # record all not fixed guids by name discarding duplicates

    for meta_file in source.rglob("*.meta"):
        relative_path = str(meta_file.relative_to(source))
        if relative_path not in guids:
            continue
        if relative_path in fixed:
            continue

        name = meta_file.name

        if name in guids_by_name:
            duplicate_names.add(name)
            continue

        guids_by_name[name] = guids[relative_path]

    for duplicate in duplicate_names:
        guids_by_name.pop(duplicate)

    duplicate_names.clear()

    # record all not fixed files by name discarding duplicates

    files_by_name = {}

    for meta_file in target.rglob("*.meta"):
        relative_path = str(meta_file.relative_to(target))
        if relative_path in fixed:
            continue

        name = meta_file.name

        if name not in guids_by_name:
            continue
        if name in files_by_name:
            duplicate_names.add(name)
            continue

        files_by_name[name] = meta_file

    for duplicate in duplicate_names:
        files_by_name.pop(duplicate)

    duplicate_names.clear()

    # finally fix everything

    for name, guid in guids_by_name.items():
        if name not in files_by_name:
            continue

        meta_file = files_by_name[name]
        new_guid = guids_by_name[name]

        text = meta_file.read_text(encoding="utf-8")
        new_text = re.sub(r"guid:\s*\S+", f"guid: {new_guid}", text, count=1)
        meta_file.write_text(new_text, encoding="utf-8")

    print("done")


main()
