# Change Log
All notable changes to this project will be documented in this file.
Changelog recommendations (http://keepachangelog.com/).
This project adheres to [Semantic Versioning](http://semver.org/).
The headings should be in this order:
- Security
- Changed
- Removed
- Deprecated
- Fixed
- Added

# UNRELEASED

## Fixed
- Parsing error that could occur if yaml content contained "..." or "---" which resulted in the parser getting confused.

# 1.2.0 - [2017-10-27]

## Changed
- You can now send a cancellation token to the ParseAsync method which allows you to cancel the parsing. If this token is cancelled it will throw a TaskCanceledException (will be recieved as AggregateException since it's thrown inside a task).

# 1.1.0 - [2017-02-10]

## Added
- Added copy of TAP stream to test session results.

# 1.0.2 - [2017-02-10]

## Fixed
- Invoke errors are now forwarded to OnError.

# 1.0.1 - [2017-02-09]

## Fixed
- Make sure the parser do not raise errors on empty lines.