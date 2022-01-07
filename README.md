#  Could not load file or assembly or one of its dependencies? Yes you can!

Have you ever wasted hours of your time trying to fix these types of errors?

```
Could not load file or assembly or one of its dependencies
Additional information: Could not load file or assembly 
'Microsoft.Practices.Unity, Version=1.2.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35' 
or one of its dependencies. The located assembly's manifest definition does not match the assembly reference. 
(Exception from HRESULT: 0x80131040)
```

```
System.IO.FileLoadException : Could not load file or assembly 
'Newtonsoft.Json, Version=4.5.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed' 
or one of its dependencies. The located assembly's manifest definition does not match the assembly reference. 
(Exception from HRESULT: 0x80131040)
```

Then, you waste even more time trying to add dependencies manually using FusLogVw, or DependencyWalker, or other utilities, only to find that other libraries in your project continue to throw these types of errors making you go around and around. Worse, these errors only show up when you actually run the code that exercises these libraries meaning you might not even find these errors until you go to production with your code causing you even more headaches. 

I've been there too many times to count, and I've had it. So, I wrote this little utility to analyze your compiled application detect all of the dependencies in it, and then figure out which ones are missing the correct version of their target assemblies in the compiled output directory. 

## Example:

Let's say that you are using System.Net.Http.Formatting (v5.2.3.0) in your project. This library requires Newtonsoft.Json v6.0.0.0. Now, if you are also using Braintree v5.9.0.0, that library needs to use Newtonsoft.Json v9.0.0.0. Oh no, what is poor .Net to do? Which one will it pick? Sadly, your application also uses Confluent.Kafka v1.5.2.0, which requires Newtonsoft.Json v11.0.0.0... Even worse, your application uses Newtonsoft.Json directly at v12.0.0.0. When your application builds, you only end up with Newtonsoft.Json v12.0.0.0 on your hard drive. Those other versions aren't available. Your application builds just fine, yet when you run it, you get those dreaded manifest definition errors, and start Googling. You quickly find StackExchange articles to add assembly binding redirects to your config files... but what versions should you put in there? You check the version of Newtonsoft.Json.dll on your hard drive, but the version in there doesn't match the version that your application is looking for, and you still get the error. You mess around a while, maybe copy someone else's config and get it working... or did you? A few hours later you get another exception, this time for System.Web.Mvc... or maybe it will be Microsoft.Bcl.AsyncInterfaces, or will it be System.Memory? And those assemblies don't even show a version when you look at those file details. Get ready to waste hours. 

## Those days are OVER!

Now, you can easily fix your assembly binding redirects with no guesswork, no dependency cross stitching and no more tears. Just run my program against your compiled binary, and point it at your config files and let it fix up your application. 

Example Syntax: 

``` 
DependencyFixup.exe [Main Assembly File] [Config Files (wildcards supported)]
```


> Fix all config files (release / debug) in your application:
```
DependencyFixup.exe "c:\source\HelloWorld\bin\Debug\HelloWorld.exe" "c:\source\HelloWorld\App*.config"
```

> Example Output
```
Transient Dependency Requested: Microsoft.Bcl.AsyncInterfaces@1.0.0.0
Redirecting to:                 Microsoft.Bcl.AsyncInterfaces@5.0.0.0
Used By:
   RedLockNet.Abstractions@2.3.1.0
   RedLockNet.SERedis@2.3.1.0


Transient Dependency Requested: Newtonsoft.Json@10.0.0.0
Redirecting to:                 Newtonsoft.Json@12.0.0.0
Used By:
   Avro@1.9.0.0


Transient Dependency Requested: Newtonsoft.Json@6.0.0.0
Redirecting to:                 Newtonsoft.Json@12.0.0.0
Used By:
   System.Net.Http.Formatting@5.2.3.0
   System.Web.Http@5.2.3.0
   Microsoft.Hadoop.Avro@1.5.6.0


Transient Dependency Requested: Newtonsoft.Json@9.0.0.0
Redirecting to:                 Newtonsoft.Json@12.0.0.0
Used By:
   Confluent.SchemaRegistry@1.5.2.0
   Braintree@5.9.0.0


Transient Dependency Requested: System.Memory@4.0.1.0
Redirecting to:                 System.Memory@4.0.1.1
Used By:
   Confluent.Kafka@1.5.2.0
   Confluent.SchemaRegistry.Serdes.Avro@1.5.2.0
   Google.Protobuf@3.13.0.0
```

> Changes applied to all of your config files
```
<?xml version="1.0"?>
<configuration>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-12.0.0.0" newVersion="12.0.0.0" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="Microsoft.Bcl.AsyncInterfaces" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-5.0.0.0" newVersion="5.0.0.0" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="System.Memory" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.0.1.1" newVersion="4.0.1.1" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>

```

I hope that you find it useful, and that it saves you lots of time! 



