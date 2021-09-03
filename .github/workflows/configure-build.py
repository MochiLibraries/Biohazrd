#!/usr/bin/env python3
import os
import re

import gha

#==================================================================================================
# Get inputs
#==================================================================================================
def get_environment_variable(name):
    ret = os.getenv(name)

    if ret is None:
        gha.print_error(f"Missing required parameter '{name}'")
    
    if (ret == ''):
        return None

    return ret

github_event_name = get_environment_variable('github_event_name')
github_run_number = get_environment_variable('github_run_number')
github_ref = get_environment_variable('github_ref')

#==================================================================================================
# Determine build settings
#==================================================================================================

# For GitHub refs besides main, include the branch/tag name in the default version string
ref_part = ''
if github_ref != 'refs/heads/main':
    ref = github_ref

    # Strip the ref prefix
    branch_prefix = 'refs/heads/'
    tag_prefix = 'refs/tags/'
    if ref.startswith(branch_prefix):
        ref = ref[len(branch_prefix):]
    elif ref.startswith(tag_prefix):
        ref = f'tag-{ref[len(tag_prefix):]}'

    # Replace illegal characters with dashes
    ref = re.sub('[^0-9A-Za-z-]', '-', ref)

    # Make the ref part
    ref_part = f'-{ref}'

# Build the default version string
version = f'0.0.0{ref_part}-ci{github_run_number}'
is_for_release = False

# Handle non-default version strings
# Make sure logic relating to is_for_release matches the publish-packages-nuget-org in the workflow
if github_event_name == 'release':
    version = get_environment_variable('release_version')
    is_for_release = True
elif github_event_name == 'workflow_dispatch':
    workflow_dispatch_version = get_environment_variable('workflow_dispatch_version')
    workflow_dispatch_will_publish_packages = get_environment_variable('workflow_dispatch_will_publish_packages')

    if workflow_dispatch_version is not None:
        version = workflow_dispatch_version

    if workflow_dispatch_will_publish_packages.lower() == 'true':
        is_for_release = True

# Trim leading v off of version if present
if version.startswith('v'):
    version = version[1:]

# Validate the version number
if not re.match('^\d+\.\d+\.\d+(-[0-9a-zA-Z.-]+)?$', version):
    gha.print_error(f"'{version}' is not a valid semver version!")

# If there are any errors at this point, make sure we exit with an error code
gha.fail_if_errors()

#==================================================================================================
# Emit MSBuild properties
#==================================================================================================
print(f"Configuring build environment to build{' and release' if is_for_release else ''} version {version}")
gha.set_environment_variable('CiBuildVersion', version)
gha.set_environment_variable('CiIsForRelease', str(is_for_release).lower())

#==================================================================================================
# Final check to exit with an error code if any errors were printed
#==================================================================================================
gha.fail_if_errors()
