#import "LegacyBlessingInstaller.h"

#import <ServiceManagement/ServiceManagement.h>

@implementation LegacyBlessingInstaller

- (BOOL)installWithAuthorization:(AuthorizationRef)authorization
                           error:(NSError **)error {
    CFErrorRef serviceError = NULL;
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    Boolean succeeded = SMJobBless(
        kSMDomainSystemLaunchd,
        CFSTR("com.cpualert.helper"),
        authorization,
        &serviceError
    );
#pragma clang diagnostic pop
    if (!succeeded && error != NULL) {
        *error = CFBridgingRelease(serviceError);
    } else if (serviceError != NULL) {
        CFRelease(serviceError);
    }
    return succeeded;
}

- (BOOL)removeJobWithAuthorization:(AuthorizationRef)authorization
                              error:(NSError **)error {
    CFErrorRef serviceError = NULL;
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    Boolean succeeded = SMJobRemove(
        kSMDomainSystemLaunchd,
        CFSTR("com.cpualert.helper"),
        authorization,
        true,
        &serviceError
    );
#pragma clang diagnostic pop
    if (!succeeded && serviceError != NULL &&
        CFErrorGetCode(serviceError) == kSMErrorJobNotFound) {
        CFRelease(serviceError);
        return YES;
    }
    if (!succeeded && error != NULL) {
        *error = CFBridgingRelease(serviceError);
    } else if (serviceError != NULL) {
        CFRelease(serviceError);
    }
    return succeeded;
}

@end
