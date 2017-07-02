"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t;
    return { next: verb(0), "throw": verb(1), "return": verb(2) };
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (_) try {
            if (f = 1, y && (t = y[op[0] & 2 ? "return" : op[0] ? "throw" : "next"]) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [0, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
var promise_1 = require("typescript-dotnet-amd/system/promises/promise");
var BusStop = (function () {
    function BusStop() {
    }
    // Sends message "request" to request.destination and returns a promise, which succeeds only when 
    // a message of type TResponse has been received with same request id "request.requestId".
    // request.requestId and request.source will be set by this method send() automatically.
    BusStop.prototype.send = function (request) {
        return __awaiter(this, void 0, promise_1.default, function () {
            return __generator(this, function (_a) {
                return [2 /*return*/, null];
            });
        });
    };
    // Sends message "request" to request.destination and returns a promise, which will succed only when 
    // a message of type TResponse has been received with same request id "request.requestId" or given timeout 
    // elapsed (see constructor parameter).
    // request.requestId and request.source will be set by this method send() automatically.
    // sendWithTimeoutAndRetry() will retry to send the request up to 10 times within the given timeout.
    // The polling intervall time will be increased be each retry, but won't be smaller than 100 ms.
    BusStop.prototype.sendWithTimeoutAndRetry = function (request) {
        return __awaiter(this, void 0, promise_1.default, function () {
            return __generator(this, function (_a) {
                return [2 /*return*/, null];
            });
        });
    };
    // Posts message "message" into bus.
    // If "message" implements interface IAddressedMessage, this method will set property message.Source automatically.
    BusStop.prototype.post = function (message) {
    };
    return BusStop;
}());
exports.BusStop = BusStop;
//# sourceMappingURL=BusStop.js.map