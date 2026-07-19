import Foundation

let delegate = HelperService()
let listener = NSXPCListener(machServiceName: "com.cpualert.helper.xpc")
listener.setConnectionCodeSigningRequirement(HelperService.appRequirement)
listener.delegate = delegate
listener.resume()
RunLoop.current.run()
