// Slice 390: Dino Reader's StoreKit 2 wrapper. Every function is a plain C function (@_cdecl) that takes its input as a UTF-8 JSON string and a
// request id and answers once through the callback with a UTF-8 JSON string, so IosPurchases.cs needs no Objective-C binding. The answers carry
// the transaction's JWS (`jws`), the proof the Worker verifies against Apple's root. Nothing here keeps state besides the transaction-updates listener.
// Error answers carry only `domain:code` (never the localized text).
import Foundation
import StoreKit
import UIKit

public typealias DinoCallback = @convention(c) (Int32, UnsafePointer<CChar>?) -> Void

private func deliver(_ callback: DinoCallback, _ requestId: Int32, _ object: [String: Any]) {
    let data = (try? JSONSerialization.data(withJSONObject: object)) ?? Data("{\"ok\":false,\"error\":\"json\"}".utf8)
    String(decoding: data, as: UTF8.self).withCString { callback(requestId, $0) }
}

private func fail(_ callback: DinoCallback, _ requestId: Int32, _ error: Error) {
    let ns = error as NSError
    deliver(callback, requestId, ["ok": false, "error": "\(ns.domain):\(ns.code)"])
}

private func millis(_ date: Date?) -> Any {
    guard let date = date else { return NSNull() }
    return Int64(date.timeIntervalSince1970 * 1000)
}

private func idsOf(_ json: UnsafePointer<CChar>) -> [String] {
    let parsed = try? JSONSerialization.jsonObject(with: Data(String(cString: json).utf8))
    return parsed as? [String] ?? []
}

// The only intro offer the plans carry is a free trial; before iOS 17.2 the offer's payment mode is not on the transaction, so an introductory offer counts as the trial.
private func isTrial(_ transaction: Transaction) -> Bool {
    if #available(iOS 17.2, *) {
        return transaction.offer?.paymentMode == .freeTrial
    }
    return transaction.offerType == .introductory
}

private func describe(_ transaction: Transaction, jws: String) -> [String: Any] {
    return [
        "productId": transaction.productID,
        "id": String(transaction.id),
        "originalId": String(transaction.originalID),
        "purchasedAt": millis(transaction.purchaseDate),
        "originalPurchasedAt": millis(transaction.originalPurchaseDate),
        "expiresAt": millis(transaction.expirationDate),
        "trial": isTrial(transaction),
        "revoked": transaction.revocationDate != nil,
        "jws": jws,
    ]
}

/// 1 when this device may make purchases (Screen Time restrictions answer 0), else 0.
@_cdecl("dino_sk_can_pay")
public func dinoSkCanPay() -> Int32 {
    return AppStore.canMakePayments ? 1 : 0
}

/// Prices: `{"ok":true,"products":[{"id","price","trial"}],"invalid":[ids]}`; `price` is the App Store's own text in the storefront's currency.
@_cdecl("dino_sk_products")
public func dinoSkProducts(_ idsJson: UnsafePointer<CChar>, _ requestId: Int32, _ callback: DinoCallback) {
    let ids = idsOf(idsJson)
    Task {
        do {
            let found = try await Product.products(for: ids)
            let products = found.map { product -> [String: Any] in
                let offer = product.subscription?.introductoryOffer
                return ["id": product.id, "price": product.displayPrice, "trial": offer?.paymentMode == .freeTrial]
            }
            let missing = ids.filter { id in !found.contains { $0.id == id } }
            deliver(callback, requestId, ["ok": true, "products": products, "invalid": missing])
        } catch {
            fail(callback, requestId, error)
        }
    }
}

/// Buys one product: `{"ok":true,"outcome":"bought|cancelled|pending|failed","transaction":{...}}`; a verified purchase is finished here (the entitlement stays in `currentEntitlements`).
@_cdecl("dino_sk_buy")
public func dinoSkBuy(_ productId: UnsafePointer<CChar>, _ requestId: Int32, _ callback: DinoCallback) {
    let id = String(cString: productId)
    Task {
        do {
            guard let product = try await Product.products(for: [id]).first else {
                deliver(callback, requestId, ["ok": true, "outcome": "failed"])
                return
            }
            switch try await product.purchase() {
            case .success(let verification):
                guard case .verified(let transaction) = verification else {
                    deliver(callback, requestId, ["ok": true, "outcome": "failed"])
                    return
                }
                let answer = describe(transaction, jws: verification.jwsRepresentation)
                await transaction.finish()
                deliver(callback, requestId, ["ok": true, "outcome": "bought", "transaction": answer])
            case .userCancelled:
                deliver(callback, requestId, ["ok": true, "outcome": "cancelled"])
            case .pending:
                deliver(callback, requestId, ["ok": true, "outcome": "pending"])
            @unknown default:
                deliver(callback, requestId, ["ok": true, "outcome": "failed"])
            }
        } catch {
            fail(callback, requestId, error)
        }
    }
}

/// What the Apple ID owns now, read silently (no password prompt): `{"ok":true,"transactions":[{...}],"latest":[{...}]}`. `transactions` are the verified, unrevoked, unexpired
/// entitlements; `latest` is the newest verified transaction of each product id given (the subscription plans), expired or not, so a lapsed plan is seen as lapsed rather than as unknown.
@_cdecl("dino_sk_entitlements")
public func dinoSkEntitlements(_ idsJson: UnsafePointer<CChar>, _ requestId: Int32, _ callback: DinoCallback) {
    let latestIds = idsOf(idsJson)
    Task {
        var items: [[String: Any]] = []
        for await result in Transaction.currentEntitlements {
            guard case .verified(let transaction) = result, transaction.revocationDate == nil else { continue }
            items.append(describe(transaction, jws: result.jwsRepresentation))
        }
        var latest: [[String: Any]] = []
        for id in latestIds {
            guard let result = await Transaction.latest(for: id), case .verified(let transaction) = result else { continue }
            latest.append(describe(transaction, jws: result.jwsRepresentation))
        }
        deliver(callback, requestId, ["ok": true, "transactions": items, "latest": latest])
    }
}

/// The Restore purchases button: asks the App Store to sync (it may ask for the Apple ID password); read `dino_sk_entitlements` afterwards. `{"ok":true}`.
@_cdecl("dino_sk_restore")
public func dinoSkRestore(_ requestId: Int32, _ callback: DinoCallback) {
    Task {
        do {
            try await AppStore.sync()
            deliver(callback, requestId, ["ok": true])
        } catch {
            fail(callback, requestId, error)
        }
    }
}

/// Apple's own "Redeem Code" sheet (iOS 16 and newer): `{"ok":true}`, or `{"ok":false,"error":"unsupported"}` where the caller opens the web address instead.
@_cdecl("dino_sk_redeem")
public func dinoSkRedeem(_ requestId: Int32, _ callback: DinoCallback) {
    Task { @MainActor in
        guard #available(iOS 16.0, *),
              let scene = UIApplication.shared.connectedScenes.compactMap({ $0 as? UIWindowScene }).first else {
            deliver(callback, requestId, ["ok": false, "error": "unsupported"])
            return
        }
        do {
            try await AppStore.presentOfferCodeRedeemSheet(in: scene)
            deliver(callback, requestId, ["ok": true])
        } catch {
            fail(callback, requestId, error)
        }
    }
}

private var updatesTask: Task<Void, Never>?

/// Starts the one `Transaction.updates` listener (renewals, Ask to Buy approvals, purchases made elsewhere): each verified one is finished and reported as `{"event":"updated","productId":...}` with request id 0.
@_cdecl("dino_sk_start_updates")
public func dinoSkStartUpdates(_ callback: DinoCallback) {
    if updatesTask != nil { return }
    updatesTask = Task.detached {
        for await result in Transaction.updates {
            guard case .verified(let transaction) = result else { continue }
            await transaction.finish()
            deliver(callback, 0, ["ok": true, "event": "updated", "productId": transaction.productID])
        }
    }
}
