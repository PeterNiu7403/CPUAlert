#import <Foundation/Foundation.h>
#import <Security/Authorization.h>

NS_ASSUME_NONNULL_BEGIN

@interface LegacyBlessingInstaller : NSObject

- (BOOL)installWithAuthorization:(AuthorizationRef)authorization
                           error:(NSError * _Nullable * _Nullable)error;

- (BOOL)removeJobWithAuthorization:(AuthorizationRef)authorization
                              error:(NSError * _Nullable * _Nullable)error;

@end

NS_ASSUME_NONNULL_END
