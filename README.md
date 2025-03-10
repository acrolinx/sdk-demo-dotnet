# Acrolinx .NET SDK Demo

This is a showcase building an automated [Acrolinx](https://www.acrolinx.com/) Integration using the [Acrolinx .NET SDK](https://github.com/acrolinx/sdk-dotnet).

For integrating the Sidebar see: [Acrolinx .NET Sidebar Demo](https://github.com/acrolinx/acrolinx-sidebar-demo-dotnet).

## Prerequisites

Please contact [Acrolinx SDK support](https://github.com/acrolinx/acrolinx-coding-guidance/blob/master/topics/sdk-support.md)
for consulting and getting your integration certified.
The tests in this SDK work with a test license on an internal Acrolinx URL.
This license is only meant for demonstration and developing purposes.
Once you finished your integration, you'll have to get a license for your integration from Acrolinx.

Acrolinx offers different other SDKs, and examples for developing integrations.

Before you start developing your own integration, you might benefit from looking into:

* [Build With Acrolinx](https://support.acrolinx.com/hc/en-us/categories/10209837818770-Build-With-Acrolinx),
* the [Guidance for the Development of Acrolinx Integrations](https://github.com/acrolinx/acrolinx-coding-guidance),
* the [Acrolinx Platform API](https://github.com/acrolinx/platform-api)
* the [Rendered Version of Acrolinx Platform API](https://acrolinxapi.docs.apiary.io/#)
* the [Acrolinx SDKs](https://github.com/acrolinx?q=sdk), and
* the [Acrolinx Demo Projects](https://github.com/acrolinx?q=demo).

## Run the Sample

1. Open [`Acrolinx.Net.Demo.sln`](Acrolinx.Net.Demo.sln) in Visual Studio 2019.
   Visual Studio Code with .NET extension can be used as an alternative.
2. Press `F5` key to run it.

## Run Project in VS Code

### **1. Set Up Environment Variables**
Before running the project, you must set the required environment variables.

To do this, run the following command:
```bash
source acro-env-vars.bash
```
This script sets the necessary API configuration, such as:

- `ACROLINX_URL`
- `ACROLINX_SSO_TOKEN`
- `ACROLINX_USERNAME`
- `ACROLINX_CLIENT_SIGNATURE`
- `ACROLINX_CONTENT_DIR` (For content processing or for automated checking, if needed)

*IMPORTANT:* Ensure that `ACROLINX_CONTENT_DIR` is set correctly before running batch or automated checks.

### **2. Build the Project**
Run the following command to build the project:
```bash
dotnet build
```
Ensure that the build completes successfully before running the project.

---

## Run in Different Modes

### **Batch Check Mode (Acrolinx.Net.Demo)**
Batch mode is used when you want to process multiple files at once.

To run batch checking:
```bash
dotnet run --project Acrolinx.Net.Demo
```
- You'll be prompted to enter a **Batch ID**.
- The batch check will process all supported files found inside the directory specified in `ACROLINX_CONTENT_DIR`.
- Once completed, it will return a **Content Analysis Dashboard** link.

---

### **Automated Check Mode (Acrolinx.Net.AutoCheck)**
Automated mode continuously watches a directory for new or modified files and checks them automatically.

To run automated checking:
```bash
dotnet run --project Acrolinx.Net.AutoCheck
```
- This mode **monitors the `ACROLINX_CONTENT_DIR` folder** and automatically checks any file that is created or modified.
- The process runs in the background and will print scorecard links when a file is processed.

### **3. Modifying Code**
If you need to modify the checking behavior, you can edit:
- **Batch Check Mode:** Modify `sdk-demo-dotnet/Acrolinx.Net.Demo/Program.cs`
- **Automated Check Mode:** Modify `sdk-demo-dotnet/Acrolinx.Net.AutoCheck/AutomatedCheckProgram.cs`

---

## License

Copyright 2019-present Acrolinx GmbH

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at:

[http://www.apache.org/licenses/LICENSE-2.0](http://www.apache.org/licenses/LICENSE-2.0)

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

