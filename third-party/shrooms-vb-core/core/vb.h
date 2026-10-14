#ifndef VB_H_
#define VB_H_

#ifdef __cplusplus
extern "C" {
#endif

#ifndef VBAPI
#define VBAPI extern
#endif

/* Header includes */
#include <stddef.h>
#include <stdint.h>



/**************************** Compilation Control ****************************/

/*
  VB_DIRECT_EXCEPTION
  VB_DIRECT_EXECUTE
  VB_DIRECT_FETCH
  VB_DIRECT_FRAME
  VB_DIRECT_LINK
  VB_DIRECT_READ
  VB_DIRECT_SAMPLES
  VB_DIRECT_WRITE

  Implements callbacks as direct function calls with names given by the above
  symbols in their respective situations. If left undefined, the function
  pointer in the simulation state is used. Whether or not to call the callback
  is still determined by whether the function pointer is NULL.
*/

/*
  VB_DIV_GENERIC

  Provides a generic implementation for the DIV instruction that guarantees the
  correct results for quotient and remainder. If left undefined, an accelerated
  implementation is used that depends on the compiler's behavior.
*/

/*
  VB_LITTLE_ENDIAN

  Accelerates simulated memory reads and writes on hosts with little-endian
  byte ordering. If left undefined, a generic implementation is used instead.
*/

/*
  VB_SIGNED_PROPAGATE

  Accelerates sign extension by assuming the >> operator propagates the sign
  bit for signed types and does not for unsigned types. If left undefined, a
  generic implementation is used instead.
*/




/********************************* Constants *********************************/

/* System registers */
#define VB_ADTRE 25
#define VB_CHCW  24
#define VB_ECR    4
#define VB_EIPC   0
#define VB_EIPSW  1
#define VB_FEPC   2
#define VB_FEPSW  3
#define VB_PIR    6
#define VB_PSW    5
#define VB_TKCW   7

/* Memory access data types */
#define VB_S8  0
#define VB_U8  1
#define VB_S16 2
#define VB_U16 3
#define VB_S32 4
#define VB_F32 5

/* Option keys */
#define VB_PSEUDO_HALT 0

/* Controller buttons */
#define VB_PWR 0x0001
#define VB_SGN 0x0002
#define VB_A   0x0004
#define VB_B   0x0008
#define VB_RT  0x0010
#define VB_LT  0x0020
#define VB_RU  0x0040
#define VB_RR  0x0080
#define VB_LR  0x0100
#define VB_LL  0x0200
#define VB_LD  0x0400
#define VB_LU  0x0800
#define VB_STA 0x1000
#define VB_SEL 0x2000
#define VB_RL  0x4000
#define VB_RD  0x8000



/*********************************** Types ***********************************/

/* Simulation state */
typedef struct VB VB;

/* Callbacks */
typedef int  (*vbOnException)(VB *sim, uint16_t *cause);
typedef int  (*vbOnExecute  )(VB *sim, uint32_t address, const uint16_t *code, int length);
typedef int  (*vbOnFetch    )(VB *sim, int fetch, uint32_t address, int32_t *value, uint32_t *cycles);
typedef int  (*vbOnFrame    )(VB *sim);
typedef int  (*vbOnLink     )(VB *sim1, VB *sim2, uint8_t *value1, uint8_t *value2);
typedef int  (*vbOnRead     )(VB *sim, uint32_t address, int type, int32_t *value, uint32_t *cycles);
typedef void (*vbOnSamples  )(VB *sim, int16_t *buffer, uint32_t capacity);
typedef int  (*vbOnWrite    )(VB *sim, uint32_t address, int type, int32_t *value, uint32_t *cycles, int *cancel);




/******************************* API Commands ********************************/

VBAPI int           vbEmulate             (VB *sim, uint32_t *clocks);
VBAPI int           vbEmulateEx           (VB **sims, unsigned count, uint32_t *clocks);
VBAPI void*         vbGetCartRAM          (VB *sim, uint32_t *size);
VBAPI void*         vbGetCartROM          (VB *sim, uint32_t *size);
VBAPI vbOnException vbGetExceptionCallback(VB *sim);
VBAPI vbOnExecute   vbGetExecuteCallback  (VB *sim);
VBAPI vbOnFetch     vbGetFetchCallback    (VB *sim);
VBAPI vbOnFrame     vbGetFrameCallback    (VB *sim);
VBAPI uint16_t      vbGetKeys             (VB *sim);
VBAPI vbOnLink      vbGetLinkCallback     (VB *sim);
VBAPI int           vbGetOption           (VB *sim, int key);
VBAPI VB*           vbGetPeer             (VB *sim);
VBAPI void          vbGetPixels           (VB *sim, void *left, int leftStrideX, int leftStrideY, void *right, int rightStrideX, int rightStrideY);
VBAPI uint32_t      vbGetProgramCounter   (VB *sim);
VBAPI int32_t       vbGetProgramRegister  (VB *sim, unsigned index);
VBAPI vbOnRead      vbGetReadCallback     (VB *sim);
VBAPI void*         vbGetSamples          (VB *sim, int *type, unsigned *capacity, unsigned *position);
VBAPI vbOnSamples   vbGetSamplesCallback  (VB *sim);
VBAPI uint32_t      vbGetSystemRegister   (VB *sim, unsigned index);
VBAPI void*         vbGetUserData         (VB *sim);
VBAPI vbOnWrite     vbGetWriteCallback    (VB *sim);
VBAPI VB*           vbInit                (VB *sim);
VBAPI int32_t       vbRead                (VB *sim, uint32_t address, int type);
VBAPI VB*           vbReset               (VB *sim);
VBAPI int           vbSetCartRAM          (VB *sim, void *sram, uint32_t size);
VBAPI int           vbSetCartROM          (VB *sim, void *rom, uint32_t size);
VBAPI vbOnException vbSetExceptionCallback(VB *sim, vbOnException callback);
VBAPI vbOnExecute   vbSetExecuteCallback  (VB *sim, vbOnExecute callback);
VBAPI vbOnFetch     vbSetFetchCallback    (VB *sim, vbOnFetch callback);
VBAPI vbOnFrame     vbSetFrameCallback    (VB *sim, vbOnFrame callback);
VBAPI vbOnLink      vbSetLinkCallback     (VB *sim, vbOnLink callback);
VBAPI uint16_t      vbSetKeys             (VB *sim, uint16_t keys);
VBAPI int           vbSetOption           (VB *sim, int key, int value);
VBAPI void          vbSetPeer             (VB *sim, VB *peer);
VBAPI uint32_t      vbSetProgramCounter   (VB *sim, uint32_t value);
VBAPI int32_t       vbSetProgramRegister  (VB *sim, unsigned index, int32_t value);
VBAPI vbOnRead      vbSetReadCallback     (VB *sim, vbOnRead callback);
VBAPI int           vbSetSamples          (VB *sim, void *samples, int type, unsigned capacity);
VBAPI vbOnSamples   vbSetSamplesCallback  (VB *sim, vbOnSamples callback);
VBAPI uint32_t      vbSetSystemRegister   (VB *sim, unsigned index, uint32_t value);
VBAPI void*         vbSetUserData         (VB *sim, void *tag);
VBAPI vbOnWrite     vbSetWriteCallback    (VB *sim, vbOnWrite callback);
VBAPI size_t        vbSizeOf              (void);
VBAPI int32_t       vbWrite               (VB *sim, uint32_t address, int type, int32_t value);



#ifdef __cplusplus
}
#endif

#endif /* VB_H_ */
