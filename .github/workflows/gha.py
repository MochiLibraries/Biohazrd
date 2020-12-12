# GitHub Actions Utility Functions
# https://docs.github.com/en/actions/reference/workflow-commands-for-github-actions
import os
import sys

errors_were_printed = False

def fail_if_errors():
    if errors_were_printed:
        print("Exiting due to previous errors.", file=sys.stderr)
        sys.exit(1)

def print_error(message):
    global errors_were_printed
    errors_were_printed = True
    print(f"::error::{message}", file=sys.stderr)

def print_warning(message):
    print(f"::warning::{message}", file=sys.stderr)

def set_output(name, value):
    if isinstance(value, bool):
        value = "true" if value else "false"
    print(f"::set-output name={name}::{value}")

def github_file_command(command, message):
    command = f"GITHUB_{command}"
    command_file = os.getenv(command)

    if command_file is None:
        print_error(f"Missing required GitHub environment variable '{command}'")
        sys.exit(1)

    if not os.path.exists(command_file):
        print_error(f"'{command}' points to non-existent file '{command_file}')")
        sys.exit(1)
    
    with open(command_file, 'a') as command_file_handle:
        command_file_handle.write(message)
        command_file_handle.write('\n')

def set_environment_variable(name, value):
    github_file_command("ENV", f"{name}={value}")

def add_path(path):
    github_file_command("PATH", path)
