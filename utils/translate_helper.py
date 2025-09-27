import sys
import os
import xml.etree.ElementTree as ET
import re

# Define namespaces URIs
XAML_NS = 'https://github.com/avaloniaui'
X_NS = 'http://schemas.microsoft.com/winfx/2006/xaml'

def register_namespaces():
    """Registers namespaces for ElementTree to use when writing the XML file."""
    ET.register_namespace('', XAML_NS)
    ET.register_namespace('x', X_NS)

def get_locale_files(lang_id):
    """Constructs the absolute paths for the target and reference locale files."""
    try:
        script_dir = os.path.dirname(os.path.realpath(__file__))
        project_root = os.path.abspath(os.path.join(script_dir, '..'))
        locales_dir = os.path.join(project_root, 'src', 'Resources', 'Locales')
    except NameError:
        project_root = os.path.abspath(os.getcwd())
        locales_dir = os.path.join(project_root, 'src', 'Resources', 'Locales')

    target_file = os.path.join(locales_dir, f"{lang_id}.axaml")

    if not os.path.exists(target_file):
        print(f"Error: Target language file not found at {target_file}")
        sys.exit(1)

    try:
        tree = ET.parse(target_file)
        root = tree.getroot()
        merged_dict = root.find(f"{{{XAML_NS}}}ResourceDictionary.MergedDictionaries")
        if merged_dict is None:
            raise ValueError("Could not find MergedDictionaries tag.")
        
        resource_include = merged_dict.find(f"{{{XAML_NS}}}ResourceInclude")
        if resource_include is None:
            raise ValueError("Could not find ResourceInclude tag.")

        include_source = resource_include.get('Source')
        ref_filename_match = re.search(r'([a-zA-Z]{2}_[a-zA-Z]{2}).axaml', include_source)
        if not ref_filename_match:
            raise ValueError("Could not parse reference filename from Source attribute.")
        
        ref_filename = f"{ref_filename_match.group(1)}.axaml"
        ref_file = os.path.join(locales_dir, ref_filename)
    except Exception as e:
        print(f"Error parsing {target_file} to find reference file: {e}")
        sys.exit(1)

    if not os.path.exists(ref_file):
        print(f"Error: Reference language file '{ref_file}' not found.")
        sys.exit(1)

    return target_file, ref_file

def get_strings(root):
    """Extracts all translation keys and their text values from an XML root."""
    strings = {}
    for string_tag in root.findall(f"{{{X_NS}}}String"):
        key = string_tag.get(f"{{{X_NS}}}Key")
        if key:
            strings[key] = string_tag.text if string_tag.text is not None else ""
    return strings

def add_new_string_tag(root, key, value):
    """Adds a new <x:String> tag to the XML root, maintaining some formatting."""
    new_tag = ET.Element(f"{{{X_NS}}}String")
    new_tag.set(f"{{{X_NS}}}Key", key)
    new_tag.set("xml:space", "preserve")
    new_tag.text = value

    last_element_index = -1
    children = list(root)
    for i in range(len(children) - 1, -1, -1):
        if (children[i].tag == f"{{{X_NS}}}String" or 
            children[i].tag == f"{{{XAML_NS}}}ResourceDictionary.MergedDictionaries"):
            last_element_index = i
            break

    if last_element_index != -1:
        new_tag.tail = root[last_element_index].tail
        root.insert(last_element_index + 1, new_tag)
    else:
        new_tag.tail = "\n  "
        root.append(new_tag)

def save_translations(tree, file_path):
    """Saves the XML tree to the file, attempting to preserve formatting."""
    try:
        ET.indent(tree, space="  ")
    except AttributeError:
        print("Warning: ET.indent not available. Output formatting may not be ideal.")
        
    tree.write(file_path, encoding='utf-8', xml_declaration=True)
    print(f"\nSaved changes to {file_path}")

def main():
    """Main function to run the translation helper script."""
    register_namespaces()

    if len(sys.argv) < 2:
        print("Usage: python utils/translate_helper.py <lang_id> [--check]")
        sys.exit(1)

    lang_id = sys.argv[1]
    is_check_mode = len(sys.argv) > 2 and sys.argv[2] == '--check'

    target_file_path, ref_file_path = get_locale_files(lang_id)

    parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    target_tree = ET.parse(target_file_path, parser)
    target_root = target_tree.getroot()
    
    ref_tree = ET.parse(ref_file_path)
    ref_root = ref_tree.getroot()

    target_strings = get_strings(target_root)
    ref_strings = get_strings(ref_root)

    missing_keys = sorted([key for key in ref_strings.keys() if key not in target_strings])

    if not missing_keys:
        print("All keys are translated. Nothing to do.")
        return

    print(f"Found {len(missing_keys)} missing keys for language '{lang_id}'.")

    if is_check_mode:
        print("Missing keys:")
        for key in missing_keys:
            print(f"  - {key}")
        return

    print("Starting interactive translation...\n")
    changes_made = False
    try:
        for i, key in enumerate(missing_keys):
            original_text = ref_strings.get(key, "")
            print("-" * 40)
            print(f"({i+1}/{len(missing_keys)}) Key: '{key}'")
            print(f"Original: '{original_text}'")

            user_input = input("Enter translation (or press Enter to skip, 'q' to save and quit): ")

            if user_input.lower() == 'q':
                print("\nQuitting and saving changes...")
                break
            elif user_input:
                add_new_string_tag(target_root, key, user_input)
                changes_made = True
                print(f"Added translation for '{key}'")

    except (KeyboardInterrupt, EOFError):
        print("\n\nProcess interrupted. Saving changes...")
    finally:
        if changes_made:
            save_translations(target_tree, target_file_path)
        else:
            print("\nNo changes were made.")

if __name__ == "__main__":
    main()